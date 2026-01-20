using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vaurioajoneuvo_finder
{
    public sealed class Oferta
    {
        public string Header { get; set; }
        public string Url { get; set; }
        public string Price { get; set; }
        public Image Img { get; set; }
        public string ImgUrl { get; set; }
        public int Year { get; set; }
    }
}
