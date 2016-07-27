using System;
using System . Collections . Generic;
using System . Linq;
using System . Runtime . Serialization;
using System . Text;
using System . Threading . Tasks;
using Windows . UI;
using Windows . UI . Xaml . Media;

namespace Waterfall . ItemPropertiesModel {
    [DataContract]
    public class RecSolidBrush {
        [DataMember]
        public Color RecColor { get; set; }
        public RecSolidBrush ( Color brush ) {
            this . RecColor = brush;
        }
    }
}
