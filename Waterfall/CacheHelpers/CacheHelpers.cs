using System;
using System . Collections . Generic;
using System . Diagnostics;
using System . IO;
using System . Linq;
using System . Text;
using System . Threading . Tasks;
using Windows . Storage;

namespace Noodles_Blog_ClassLibrary . Helpers {
    /// <summary>
    /// 在整个解决方案中使用的Cache value字符串常量的集合 ///
    /// 此文件向所有项目共享
    /// </summary>
    public static class CacheHelpers {
        private const string example = "EXAMPLE";
        private const string RectangleColorList = "REC_COLOR_LIST";

        /// <summary>
        /// 读取缓存文件 ///
        /// </summary>
        public static async Task<string> ReadResetCacheValue ( string key ) {
            #if DEBUG
            //Debug . WriteLine ( "\n读取缓存-----> ：【 " + key + " 】" );
            #endif
            var localFolder = ApplicationData . Current . LocalCacheFolder;
            StorageFile file = default ( StorageFile );
            try {
                switch ( key ) {
                    case example:
                        file = await localFolder . GetFileAsync ( "example.txt" );
                        break;
                    case RectangleColorList:
                        file = await localFolder . GetFileAsync ( "RectangleColorList.txt" );
                        break;
                    default:
                        #if DEBUG
                        Debug . WriteLine ( "----->【 没有匹配的读取指令】" );
                        #endif
                        return null;
                }
                string value = await FileIO . ReadTextAsync ( file );
                #if DEBUG
                //Debug . WriteLine ( "-----> 【 缓存数据读取完毕 】" );
                #endif
                return value;
            } catch ( FileNotFoundException ) { Debug . WriteLine ( "Error -----> 【 读取缓存文件失败 】" ); return null; }
        }

        /// <summary>
        /// 设置缓存文件 ///
        /// </summary>
        public static async Task SaveCacheValue ( string key , object value ) {
            #if DEBUG
            Debug . WriteLine ( "\n存储缓存【 " + key + " 】 -----> :【 " + ( value == null ? "不存在指定键值 】" : ( value . ToString ( ) + " 】" ) ) );
            #endif
            var localFolder = ApplicationData . Current . LocalCacheFolder;
            StorageFile file = default ( StorageFile );
            switch ( key ) {
                case example:
                    file = await localFolder . CreateFileAsync ( "example.txt" , CreationCollisionOption . ReplaceExisting );
                    break;
                case RectangleColorList:
                    file = await localFolder . CreateFileAsync ( "RectangleColorList.txt" , CreationCollisionOption . ReplaceExisting );
                    break;
                default:
                    #if DEBUG
                    Debug . WriteLine ( "Error -----> 【 缓存文件写入失败 】" );
                    #endif
                    return;
            }
            await FileIO . WriteTextAsync ( file , value . ToString ( ) );
            #if DEBUG
            Debug . WriteLine ( " -----> 【 数据写入缓存完毕 】" );
            #endif
        }

        /// <summary>
        /// 读取特殊缓存文件 ///
        /// </summary>
        public static async Task<string> ReadSpecialCacheValue ( string key ) {
            Debug . WriteLine ( "\n读取缓存-----> ：【 " + key + " 】" );
            var localFolder = ApplicationData . Current . LocalCacheFolder;
            StorageFile file = default ( StorageFile );
            try {
                file = await localFolder . GetFileAsync ( key + "_cache.txt" );
                string value = await FileIO . ReadTextAsync ( file );
                Debug . WriteLine ( "-----> 【 缓存文件读取成功 】" );
                return value;
            } catch ( FileNotFoundException ) { Debug . WriteLine ( "Error -----> 【 数据读出缓存失败 】" ); return null; }
        }

        /// <summary>
        /// 设置特殊缓存文件 ///
        /// </summary>
        public static async Task SaveSpecialCacheValue ( string key , object value ) {
            Debug . WriteLine ( "\n存储缓存【 " + key + " 】 -----> :【 " + ( value == null ? "不存在指定键值 】" : ( value . ToString ( ) + " 】" ) ) );
            var localFolder = ApplicationData . Current . LocalCacheFolder;
            StorageFile file = default ( StorageFile );
            file = await localFolder . CreateFileAsync ( key + "_cache.txt" , CreationCollisionOption . ReplaceExisting );
            await FileIO . WriteTextAsync ( file , value . ToString ( ) );
            Debug . WriteLine ( "-----> 【 据写入缓存完毕 】" );
        }
    }
}
