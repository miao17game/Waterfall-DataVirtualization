using System;
using System . Collections;
using System . Collections . Generic;
using Windows . UI . Xaml . Data;

namespace Waterfall . DataVirtualization {

    /// <summary>
    /// 表示已排序的集合的不连续的 ItemIndexRanges
    /// </summary>
    class ItemIndexRangeList : IList<ItemIndexRange> {
        private List<ItemIndexRange> _ranges;

        public ItemIndexRangeList ( ) {
            this . _ranges = new List<ItemIndexRange> ( );
        }

        public ItemIndexRangeList ( List<ItemIndexRange> ranges ) {
            this . _ranges = NormalizeRanges ( ranges );
        }

        public ItemIndexRangeList ( ItemIndexRange [ ] ranges ) {
            this . _ranges = NormalizeRanges ( ranges );
        }

        public List<ItemIndexRange> ToList ( ) {
            return this . _ranges;
        }

        public ItemIndexRange [ ] ToArray ( ) {
            return this . _ranges . ToArray ( );
        }

        /// <summary>
        /// 将范围合并到 rangelist，结合现有的范围，如有必要
        /// </summary>
        /// <param name="newrange">要合并到集合范围</param>
        public void Add ( ItemIndexRange newrange ) {
            for ( int i = 0 ; i < this . _ranges . Count ; i++ ) {
                ItemIndexRange existing = this . _ranges [ i ];
                if ( newrange . ContiguousOrOverlaps ( existing ) ) {
                    existing = existing . Combine ( newrange );
                    for ( int j = i + 1 ; j < this . _ranges . Count ; j++ ) {
                        ItemIndexRange next = this . _ranges [ j ];
                        if ( existing . ContiguousOrOverlaps ( next ) ) {
                            existing = existing . Combine ( next );
                            this . _ranges . RemoveAt ( i + 1 );
                        }
                    }
                    this . _ranges [ i ] = existing;
                    return;
                } else if ( newrange . LastIndex < existing . FirstIndex ) {
                    this . _ranges . Insert ( i , newrange );
                    return;
                }
            }
            this . _ranges . Add ( newrange );
        }

        /// <summary>
        /// 将范围合并到 rangelist，结合现有的范围，如有必要
        /// </summary>
        public void Add ( uint FirstIndex , uint Length ) {
            this . Add ( new ItemIndexRange ( ( int ) FirstIndex , Length ) );
        }

        /// <summary>
        /// 范围从集合中移除，拆分现有范围，如有必要
        /// </summary>
        public void Subtract ( ItemIndexRange range ) {
            for ( int idx = 0 ; idx < this . _ranges . Count ; idx++ ) {
                ItemIndexRange existing = this . _ranges [ idx ];
                if ( existing . FirstIndex > range . LastIndex )
                    return;

                int i, j;
                i = Math . Max ( existing . FirstIndex , range . FirstIndex );
                j = Math . Min ( existing . LastIndex , range . LastIndex );

                if ( i <= j ) {
                    if ( existing . FirstIndex < i && existing . LastIndex > j ) {
                        /// 范围是在现有的范围，所以拆分成两个现有
                        this . _ranges [ idx ] = ( new ItemIndexRange ( existing . FirstIndex , ( uint ) ( i - existing . FirstIndex ) ) );
                        this . _ranges . Insert ( idx + 1 , new ItemIndexRange ( j + 1 , ( uint ) ( existing . LastIndex - j ) ) );
                        return;
                    } else if ( existing . LastIndex > j ) {
                        /// 范围结束前存在所以修剪现有要剩下的人
                        this . _ranges [ idx ] = new ItemIndexRange ( j + 1 , ( uint ) ( existing . LastIndex - j ) );
                        return;
                    } else if ( existing . FirstIndex < i ) {
                        /// 范围从现有所以修剪现有范围前部分之后开始
                        this . _ranges [ idx ] = new ItemIndexRange ( existing . FirstIndex , ( uint ) ( i - existing . FirstIndex ) );
                    } else {
                        /// 存在重叠的范围，所以将其删除。
                        this . _ranges . RemoveAt ( idx );
                    }
                    /// 修剪减去的范围到其余部分，并退出如果完整
                    if ( range . LastIndex > j ) {
                        range = new ItemIndexRange ( j + 1 , ( uint ) ( range . LastIndex - j ) );
                    } else { return; }
                }
            }
        }

        /// <summary>
        /// 范围从集合中移除，拆分现有范围，如有必要
        /// </summary>
        public void Subtract ( uint FirstIndex , uint Length ) {
            this . Subtract ( new ItemIndexRange ( ( int ) FirstIndex , Length ) );
        }

        /// <summary>
        /// 判断是否存在交集
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public bool Intersects ( ItemIndexRange range ) {
            foreach ( ItemIndexRange r in this . _ranges ) {
                if ( r . Intersects ( range ) ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 将合并邻接或重叠的范围，以确保不是连续的集合,也因此，他们开始在索引顺序排序范围
        /// </summary>
        /// <param name="ranges"></param>
        /// <returns></returns>
        private List<ItemIndexRange> NormalizeRanges ( IEnumerable<ItemIndexRange> ranges ) {
            List<ItemIndexRange> results = new List<ItemIndexRange> ( );
            foreach ( ItemIndexRange range in ranges ) {
                bool handled = false;
                for ( int i = 0 ; i < results . Count ; i++ ) {
                    ItemIndexRange existing = results [ i ];
                    if ( range . ContiguousOrOverlaps ( existing ) ) {
                        results [ i ] = existing . Combine ( range );
                        handled = true;
                        break;
                    } else if ( range . FirstIndex < existing . FirstIndex ) {
                        results . Insert ( i , range );
                        handled = true;
                        break;
                    }
                }
                if ( !handled )
                    results . Add ( range );
            }
            return results;
        }

        #region IList 实现
        public int Count { get { return ( ( IList<ItemIndexRange> ) _ranges ) . Count; } }

        public bool IsReadOnly { get { return ( ( IList<ItemIndexRange> ) _ranges ) . IsReadOnly; } }

        public ItemIndexRange this [ int index ] {
            get { return ( ( IList<ItemIndexRange> ) _ranges ) [ index ]; }
            set { ( ( IList<ItemIndexRange> ) _ranges ) [ index ] = value; }
        }


        public IEnumerator<ItemIndexRange> GetEnumerator ( ) {
            return ( ( IEnumerable<ItemIndexRange> ) _ranges ) . GetEnumerator ( );
        }

        IEnumerator IEnumerable.GetEnumerator ( ) {
            return ( ( IEnumerable<ItemIndexRange> ) _ranges ) . GetEnumerator ( );
        }

        public int IndexOf ( ItemIndexRange item ) {
            return ( ( IList<ItemIndexRange> ) _ranges ) . IndexOf ( item );
        }

        public void Insert ( int index , ItemIndexRange item ) {
            ( ( IList<ItemIndexRange> ) _ranges ) . Insert ( index , item );
        }

        public void RemoveAt ( int index ) {
            ( ( IList<ItemIndexRange> ) _ranges ) . RemoveAt ( index );
        }

        public void Clear ( ) {
            ( ( IList<ItemIndexRange> ) _ranges ) . Clear ( );
        }

        public bool Contains ( ItemIndexRange item ) {
            return ( ( IList<ItemIndexRange> ) _ranges ) . Contains ( item );
        }

        public void CopyTo ( ItemIndexRange [ ] array , int arrayIndex ) {
            ( ( IList<ItemIndexRange> ) _ranges ) . CopyTo ( array , arrayIndex );
        }

        public bool Remove ( ItemIndexRange item ) {
            return ( ( IList<ItemIndexRange> ) _ranges ) . Remove ( item );
        }

        #endregion
    }
}
