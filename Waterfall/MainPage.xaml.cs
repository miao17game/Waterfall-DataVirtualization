using System;
using System . Collections . Generic;
using System . Diagnostics;
using System . IO;
using System . Linq;
using System . Runtime . InteropServices . WindowsRuntime;
using Waterfall . DataVirtualization;
using Windows . Foundation;
using Windows . Foundation . Collections;
using Windows . UI . Xaml;
using Windows . UI . Xaml . Controls;
using Windows . UI . Xaml . Controls . Primitives;
using Windows . UI . Xaml . Data;
using Windows . UI . Xaml . Input;
using Windows . UI . Xaml . Media;
using Windows . UI . Xaml . Navigation;
using Windows . UI . Xaml . Shapes;

namespace Waterfall {
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page {
        public MainPage ( ) {
            this . InitializeComponent ( );
            Grid1 . ContainerContentChanging += Grid1_ContainerContentChanging;
            initdata ( );
        }

        private void Grid1_ContainerContentChanging ( ListViewBase sender , ContainerContentChangingEventArgs args ) {
            if ( !args . InRecycleQueue ) {
                // 在每个项目，以指示其索引设置 textblock
                FrameworkElement ctr = ( FrameworkElement ) args . ItemContainer . ContentTemplateRoot;
                if ( ctr != null ) {
                    TextBlock t = ( TextBlock ) ctr . FindName ( "idx" );
                    t . Text = args . ItemIndex . ToString ( );
                }
            }
        }

        async void initdata ( ) {
            VirtuDataSource dataSource = await VirtuDataSource . GetDataSoure ( );
            if ( dataSource . Count > 0 ) {
                Grid1 . ItemsSource = dataSource;
            } else {
                Debug . WriteLine ( "Error: The pictures folder doesn't contain any files" );
            }
        }
    }
}
