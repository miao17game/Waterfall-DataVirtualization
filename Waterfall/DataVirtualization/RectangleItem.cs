using System;
using System . Collections . Generic;
using System . Collections . Specialized;
using System . Linq;
using System . Text;
using System . Threading . Tasks;
using Windows . Storage;
using Windows . UI . Xaml . Data;
using Windows . UI . Xaml . Media . Imaging;
using System . Diagnostics;
using Windows . Storage . Search;
using Windows . Storage . FileProperties;
using Windows . UI . Core;
using System . Threading;
using Windows . UI;
using Windows . UI . Xaml . Media;
using Waterfall . ItemPropertiesModel;
using System . Collections . ObjectModel;
using Noodles_Blog_ClassLibrary . Helpers;

namespace Waterfall . DataVirtualization {
    /// <summary>
    /// 代表Rectangle及其属性
    /// </summary>
    class RectangleItem {
        /// <summary>
        /// 颜色色刷
        /// </summary>
        public SolidColorBrush color { get; set; }
        
        public RectangleItem ( ) { }

        public RectangleItem ( int num ) {
            this . color = new SolidColorBrush ( ConvertRandom ( num % 10 ) );
        }

        public RectangleItem ( SolidColorBrush brush ) {
            this . color = brush;
        }

        /// <summary>
        /// 需要确保只有一个请求正在进行一次 , 这里用不到
        /// </summary>
        private static SemaphoreSlim gettingFileProperties = new SemaphoreSlim(1);

        /// <summary>
        /// 获取指定文件的所有数据
        /// </summary>
        /// <param name="file"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static RectangleItem fromStorageFile ( CancellationToken token ) {
            RectangleItem item = new RectangleItem ( );
            item . color = new SolidColorBrush ( ConvertRandom ( new Random ( ) . Next ( ) % 10 ) );
            return item;
        }

        /// <summary>
        /// 获取指定文件的所有数据
        /// </summary>
        /// <param name="file"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static RectangleItem fromStorageFile ( ) {
            RectangleItem item = new RectangleItem ( );
            item . color = new SolidColorBrush ( ConvertRandom ( new Random ( ) . Next ( ) % 5 ) );
            return item;
        }

        /// <summary>
        /// 初始化资源
        /// </summary>
        /// <param name="TargetSource"></param>
        /// <returns></returns>
        public static async Task InitAndSaveSource ( ObservableCollection<RectangleItem> TargetSource ) {
            var CacheList = new List<RecSolidBrush> ( );
            Random newRan = new Random ( );
            for ( int i = 0 ; i < 3000 ; i++ ) {
                var item = new RectangleItem ( newRan . Next ( ) );
                CacheList . Add ( new RecSolidBrush ( item . color . Color ) );
                TargetSource . Add ( item );
            }
            await CacheHelpers . SaveCacheValue ( CacheConstants . RectangleColorList , JsonHelper . ToJson ( CacheList ) );
        }

        /// <summary>
        /// 刷新当前视口并存储
        /// </summary>
        /// <param name="batch"></param>
        /// <param name="token"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        public static async Task<RectangleItem [ ]> UpdateCacheResources ( int FirstIndex , int LastIndex , CancellationToken token , int results ) {
            var listString = await CacheHelpers . ReadResetCacheValue ( CacheConstants . RectangleColorList );
            List<RecSolidBrush> colorList = string . IsNullOrEmpty ( listString ) ? null : JsonHelper . FromJson<List<RecSolidBrush>> ( listString );
            RecSolidBrush [ ] newList = new RecSolidBrush [ results ];
            Array . Copy ( colorList . ToArray ( ) , FirstIndex , newList , 0 , results );
            /// 应用读取到的数据
            List<RectangleItem> files = new List<RectangleItem> ( );
            if ( results != 0 ) {
                for ( int i = 0 ; i < results ; i++ ) {
                    /// 检查是否请求已被取消，如果中止，则中止获取额外数据
                    token . ThrowIfCancellationRequested ( );
                    RectangleItem newItem = colorList == null ? fromStorageFile ( token ) : new RectangleItem ( new SolidColorBrush ( newList [ i ] . RecColor ) );
                    files . Add ( newItem );
                }
            }
            return files . ToArray ( );
        }

        #region 随机数%-->颜色的映射
        /// <summary>
        /// 将随机数指定为色刷
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        static Color ConvertRandom ( int num ) {
            return RandomNumbersColorMap . ContainsKey ( num ) ? RandomNumbersColorMap [ num ] : RandomNumbersColorMap [ 0 ];
        }

        private static Dictionary<int,Color> RandomNumbersColorMap = new Dictionary<int, Color> {
            { 1, Colors . Beige},
            { 2, Colors . DarkCyan},
            { 3, Colors . WhiteSmoke},
            { 4, Colors . GreenYellow},
            { 5, Colors . PaleGoldenrod},
            { 6, Colors . Purple},
            { 7, Colors . Aquamarine},
            { 8, Colors . CornflowerBlue},
            { 9, Colors . Magenta},
            { 0, Colors . Salmon},
        };
        #endregion
    }
}
