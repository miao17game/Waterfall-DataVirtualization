using System;
using System . Collections . Generic;
using System . Diagnostics;
using System . Threading;
using System . Threading . Tasks;
using Windows . Foundation;
using Windows . UI . Xaml;
using Windows . UI . Xaml . Data;

namespace Waterfall . DataVirtualization {
    /// <summary>
    /// 实现一个相对简单的缓存项目基于一组的范围 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class ItemCacheHandler<T> {
        /// <summary>
        /// 数据结构，用于保存在范围是缓存中的所有项目
        /// </summary>
        private List<CacheEntryBlock<T>> WholeCacheBlocks;
        /// <summary>
        /// 在高速缓存中不存在的项目范围的列表
        /// </summary>
        internal ItemIndexRangeList RequestsNotInSpeedCache;
        /// <summary>
        /// 范围是在缓存中的项目列表
        /// </summary>
        private ItemIndexRangeList ResultsInCache;
        /// <summary>
        /// 目前正在请求的范围的项目
        /// </summary>
        private ItemIndexRange RequestInProgressing;
        /// <summary>
        /// 用于将用于取消未完成的请求
        /// </summary>
        private CancellationTokenSource CancelTokenSource;
        /// <summary>
        /// 将用于请求数据的回调
        /// </summary>
        private FetchDataCallbackHandler FetchCallback;
        /// <summary>
        /// 最多可以在一个批处理中读取的项目数
        /// </summary>
        public int MaxBatchFetchSize = 50;
        /// <summary>
        /// 计时器，它用于延迟读取数据，如果列表快速滚动，以便我们能赶上
        /// </summary>
        private DispatcherTimer timer;

        #if DEBUG
        /// <summary>
        /// 名字用于跟踪消息和调试
        /// </summary>
        string debugName = string.Empty;
        #endif

        /// <summary>
        /// 构造缓存管理
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="batchsize"></param>
        /// <param name="debugName"></param>
        public ItemCacheHandler ( FetchDataCallbackHandler callback , int batchsize , string debugName = "ItemCacheManager" ) {
            WholeCacheBlocks = new List<CacheEntryBlock<T>> ( );
            RequestsNotInSpeedCache = new ItemIndexRangeList ( );
            ResultsInCache = new ItemIndexRangeList ( );
            FetchCallback = callback;
            MaxBatchFetchSize = batchsize;
            timer = new DispatcherTimer ( );
            timer . Tick += ( sender , args ) => { fetchData ( ); };
            timer . Interval = new TimeSpan ( 20*10000 );

            #if DEBUG
            this . debugName = debugName;
            #endif
            #if TRACE_DATASOURCE
            Debug.WriteLine(debugName + "* Cache initialized/reset");
            #endif
        }

        public delegate Task<T [ ]> FetchDataCallbackHandler ( ItemIndexRange range , CancellationToken ct );

        public event TypedEventHandler<object, CacheChangedEventArgs<T>> CacheChanged;

        /// <summary>
        /// 索引器对项目缓存的访问
        /// </summary>
        /// <param name="index">Item Index</param>
        /// <returns></returns>
        public T this [ int index ] {
            get {
                /// 循环访问缓存块，以找到的项
                foreach ( CacheEntryBlock<T> block in WholeCacheBlocks ) {
                    if ( index >= block . FirstIndex && index <= block . lastIndex ) {
                        return block . Items [ index - block . FirstIndex ];
                    }
                }
                return default ( T );
            }
            set {
                /// 循环访问高速缓存块，以找到正确的块
                for ( int i = 0 ; i < WholeCacheBlocks . Count ; i++ ) {
                    CacheEntryBlock<T> block = WholeCacheBlocks [ i ];
                    if ( index >= block . FirstIndex && index <= block . lastIndex ) {
                        block . Items [ index - block . FirstIndex ] = value;
                        /// 将数值加入缓存集合
                        if ( value != null ) { ResultsInCache . Add ( ( uint ) index , 1 ); }
                        return;
                    }
                    /// 我们已经过了目标的位置
                    if ( block . FirstIndex > index ) { AddOrExtendBlock ( index , value , i ); return;
                    }
                }
                /// 不存在此位置，需要加入到Block中
                AddOrExtendBlock ( index , value , WholeCacheBlocks . Count );
            }
        }

        /// <summary>
        /// 扩展现有的块，如果该项目适合在结束时，或创建一个新的块
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <param name="insertBeforeBlock"></param>
        private void AddOrExtendBlock ( int index , T value , int insertBeforeBlock ) {
            if ( insertBeforeBlock > 0 ) {
                CacheEntryBlock<T> block = WholeCacheBlocks [ insertBeforeBlock - 1 ];
                /// 拓展
                if ( block . lastIndex == index - 1 ) {
                    T [ ] newItems = new T [ block . Length + 1 ];
                    Array . Copy ( block . Items , newItems , ( int ) block . Length );
                    newItems [ block . Length ] = value;
                    block . Length++;
                    block . Items = newItems;
                    return;
                }
            }
            /// 新建，并注入
            CacheEntryBlock<T> newBlock = new CacheEntryBlock<T> ( ) { FirstIndex = index , Length = 1 , Items = new T [ ] { value } };
            WholeCacheBlocks . Insert ( insertBeforeBlock , newBlock );
        }

        /// <summary>
        /// 更新缓存，丢弃不需要的项目，搞清楚哪些项目需要请求所需的项目的范围。如果需要，进行获取 。
        /// </summary>
        /// <param name="RangesToCache">应进行缓存的新范围集合</param>
        public void UpdateRanges ( ItemIndexRange [ ] RangesToCache ) {
            /// 将范围集合进行精炼
            RangesToCache = NormalizeRanges ( RangesToCache );
            /// 内容未变化，则不做修改
            if ( !HasRangesChanged ( RangesToCache ) ) { return; }
            /// 为了使缓存容易更新，我们将创建一套新的 CacheEntryBlocks
            List<CacheEntryBlock<T>> newCacheBlocks = new List<CacheEntryBlock<T>> ( );
            foreach ( ItemIndexRange range in RangesToCache ) {
                CacheEntryBlock<T> newBlock = new CacheEntryBlock<T> ( ) { FirstIndex = range . FirstIndex , Length = range . Length , Items = new T [ range . Length ] };
                newCacheBlocks . Add ( newBlock );
            }

            #if TRACE_DATASOURCE
            string s = "┌ " + debugName + ".UpdateRanges: ";
            foreach (ItemIndexRange range in ranges)
            {
                s += range.FirstIndex + "->" + range.LastIndex + " ";
            }
            Debug.WriteLine(s);
            #endif

            /// 从旧到新的缓存块数据副本哪里有重叠
            int lastTransferred = 0;
            for ( int i = 0 ; i < RangesToCache . Length ; i++ ) {
                CacheEntryBlock<T> newBlock = newCacheBlocks [ i ];
                ItemIndexRange range = RangesToCache [ i ];
                int j = lastTransferred;
                while ( j < this . WholeCacheBlocks . Count && this . WholeCacheBlocks [ j ] . FirstIndex <= RangesToCache [ i ] . LastIndex ) {
                    ItemIndexRange overlap, oldEntryRange;
                    ItemIndexRange [ ] added, removed;
                    CacheEntryBlock<T> oldBlock = this . WholeCacheBlocks [ j ];
                    oldEntryRange = new ItemIndexRange ( oldBlock . FirstIndex , oldBlock . Length );
                    bool hasOverlap = oldEntryRange . DiffRanges ( range , out overlap , out removed , out added );
                    if ( hasOverlap ) {
                        Array . Copy ( 
                            oldBlock . Items , 
                            overlap . FirstIndex - oldBlock . FirstIndex , 
                            newBlock . Items , 
                            overlap . FirstIndex - range . FirstIndex ,
                            ( int ) overlap . Length );

                        #if TRACE_DATASOURCE
                        Debug.WriteLine("│ Transfering cache items " + overlap.FirstIndex + "->" + overlap.LastIndex);
                        #endif

                    }
                    j++;
                    if ( RangesToCache . Length > i + 1 && oldBlock . lastIndex < RangesToCache [ i + 1 ] . FirstIndex ) { lastTransferred = j; }
                }
            }
            /// 在换用新的缓存
            this . WholeCacheBlocks = newCacheBlocks;

            /// 找出项目需要 , 因为我们不让它们在高速缓存中读取
            this . RequestsNotInSpeedCache = new ItemIndexRangeList ( RangesToCache );
            ItemIndexRangeList newCachedResults = new ItemIndexRangeList ( );

            /// 使用我们已缓存的先前内容，形成新的列表
            foreach ( ItemIndexRange range in RangesToCache ) {
                foreach ( ItemIndexRange cached in this . ResultsInCache ) {
                    ItemIndexRange overlap;
                    ItemIndexRange [ ] added, removed;
                    bool hasOverlap = cached . DiffRanges ( range , out overlap , out removed , out added );
                    if ( hasOverlap ) { newCachedResults . Add ( overlap ); }
                }
            }
            /// 删除的数据，我们知道我们已缓存的结果
            foreach ( ItemIndexRange range in newCachedResults ) {
                this . RequestsNotInSpeedCache . Subtract ( range );
            }
            this . ResultsInCache = newCachedResults;

            startFetchData ( );

            #if TRACE_DATASOURCE
            s = "└ Pending requests: ";
            foreach (ItemIndexRange range in this.requests)
            {
                s += range.FirstIndex + "->" + range.LastIndex + " ";
            }
            Debug.WriteLine(s);
            #endif 
        }

        /// <summary>
        /// 比较新的范围，观察集合是否被改变
        /// </summary>
        /// <param name="ranges"></param>
        /// <returns></returns>
        private bool HasRangesChanged ( ItemIndexRange [ ] ranges ) {
            if ( ranges . Length != WholeCacheBlocks . Count ) { return true; }
            for ( int i = 0 ; i < ranges . Length ; i++ ) {
                ItemIndexRange r = ranges [ i ];
                CacheEntryBlock<T> block = this . WholeCacheBlocks [ i ];
                if ( r . FirstIndex != block . FirstIndex || r . LastIndex != block . lastIndex ) { return true; }
            } return false;
        }

        /// <summary>
        /// 获取第一个在缓存中不存在的范围
        /// </summary>
        /// <param name="maxsize"></param>
        /// <returns></returns>
        public ItemIndexRange GetFirstRequestBlock ( int maxsize ) {
            if ( this . RequestsNotInSpeedCache . Count > 0 ) {
                ItemIndexRange range = this . RequestsNotInSpeedCache [ 0 ];
                if ( range . Length > MaxBatchFetchSize )
                    range = new ItemIndexRange ( range . FirstIndex , ( uint ) MaxBatchFetchSize );
                return range;
            }
            return null;
        }

        /// <summary>
        /// 节流的函数来取数据。部队的 20ms年前发出请求的等待 ,
        /// 如果在这段时间请求另一个读取，则它将重置计时器，所以我们不获取数据，只滚动视图
        /// </summary>
        public void startFetchData ( ) {
            /// 验证是否仍需要积极的请求
            if ( this . RequestInProgressing != null ) {
                if ( this . RequestsNotInSpeedCache . Intersects ( RequestInProgressing ) ) {
                    return;
                } else {
                    /// 取消现行的请求

                    #if TRACE_DATASOURCE
                    Debug.WriteLine("> " + debugName + " Cancelling request: " + requestInProgress.FirstIndex + "->" + requestInProgress.LastIndex);
                    #endif
                    CancelTokenSource . Cancel ( );

                }
            }

            /// 使用计时器来延迟由 20ms，提取数据，如果在这段时间来了另一个范围，然后的计时器重置。
            timer . Stop ( );
            timer . Start ( );
        }

        /// <summary>
        /// 通过计时器，使数据请求调用
        /// </summary>
        public async void fetchData ( ) {
            /// 停止计时器，所以我们不会被操作 , 除非请求数据
            timer . Stop ( );
            if ( this . RequestInProgressing != null ) {
                ///  验证是否仍需要积极的请求
                if ( this . RequestsNotInSpeedCache . Intersects ( RequestInProgressing ) ) {
                    return;
                } else {
                    /// 取消现行的请求
                    Debug . WriteLine ( ">" + debugName + " Cancelling request: " + RequestInProgressing . FirstIndex + "->" + RequestInProgressing . LastIndex );
                    CancelTokenSource . Cancel ( );
                }
            }

            ItemIndexRange nextRequest = GetFirstRequestBlock ( MaxBatchFetchSize );
            if ( nextRequest != null ) {
                CancelTokenSource = new CancellationTokenSource ( );
                CancellationToken ct = CancelTokenSource . Token;
                RequestInProgressing = nextRequest;
                T [ ] data = null;
                try {
                    
                    #if TRACE_DATASOURCE
                    Debug.WriteLine(">" + debugName + " Fetching items " + nextRequest.FirstIndex + "->" + nextRequest.LastIndex);
                    #endif

                    /// 使用回调来获取数据，在取消标记传递
                    data = await FetchCallback ( nextRequest , ct );

                    if ( !ct . IsCancellationRequested ) {

                        #if TRACE_DATASOURCE
                        Debug.WriteLine(">" + debugName + " Inserting items into cache at: " + nextRequest.FirstIndex + "->" + (nextRequest.FirstIndex + data.Length - 1));
                        #endif

                        for ( int i = 0 ; i < data . Length ; i++ ) {
                            int cacheIndex = ( int ) ( nextRequest . FirstIndex + i );

                            T oldItem = this [ cacheIndex ];
                            T newItem = data [ i ];

                            if ( !newItem . Equals ( oldItem ) ) {
                                this [ cacheIndex ] = newItem;

                                /// 释放 CacheChanged，因此，数据源可以激发其 INCC 事件，和做其他工作的基础有数据的项目
                                CacheChanged?.Invoke ( this , new CacheChangedEventArgs<T> ( ) { oldItem = oldItem , newItem = newItem , itemIndex = cacheIndex } );
                            }
                        }
                        RequestsNotInSpeedCache . Subtract ( new ItemIndexRange ( nextRequest . FirstIndex , ( uint ) data . Length ) );
                    }
                }
                /// Try/Catch 被需要取消是通过异常
                catch ( OperationCanceledException ) { } finally {
                    RequestInProgressing = null;
                    /// 如果需要，启动另一个请求
                    fetchData ( );
                }
            }
        }


        /// <summary>
        /// 范围精炼，剔除冗余部分，并同时保持每个集合元素的范围彼此严格离散 //
        /// 传入 ：一段范围数组   //
        /// 输出：一个经过合并的不重叠范围组成的数组 
        /// </summary>
        /// <param name="rangesToUnit">要合并范围的数组列表</param>
        /// <returns>已经合并的范围集</returns>
        private ItemIndexRange [ ] NormalizeRanges ( ItemIndexRange [ ] rangesToUnit ) {
            List<ItemIndexRange> results = new List<ItemIndexRange> ( );
            foreach ( ItemIndexRange singleRange in rangesToUnit ) {
                bool handled = false;
                for ( int i = 0 ; i < results . Count ; i++ ) {
                    ItemIndexRange existingRange = results [ i ];
                    if ( singleRange . ContiguousOrOverlaps ( existingRange ) ) {
                        results [ i ] = existingRange . Combine ( singleRange );
                        handled = true;
                        break;
                    } else if ( singleRange . FirstIndex < existingRange . FirstIndex ) {
                        results . Insert ( i , singleRange );
                        handled = true;
                        break;
                    }
                }
                if ( !handled ) { results . Add ( singleRange ); }
            }
            return results . ToArray ( );
        }


        /// <summary>
        /// 看到是否该值在我们缓存中，是则返回索引
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int IndexOf ( T value ) {
            foreach ( CacheEntryBlock<T> entry in WholeCacheBlocks ) {
                int index = Array . IndexOf<T> ( entry . Items , value );
                if ( index != -1 )
                    return index + entry . FirstIndex;
            }
            return -1;
        }

        /// <summary>
        /// 缓存块
        /// </summary>
        /// <typeparam name="ITEMTYPE">内部集合元素类型</typeparam>
        class CacheEntryBlock<ITEMTYPE> {
            public int FirstIndex;
            public uint Length;
            public ITEMTYPE[] Items;

            public int lastIndex { get { return FirstIndex + ( int ) Length - 1; } }
        }
    }

    /// <summary>
    /// CacheChanged 事件的 EventArgs 类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CacheChangedEventArgs<T> : EventArgs {
        public T oldItem { get; set; }
        public T newItem { get; set; }
        public int itemIndex { get; set; }
    }
}
