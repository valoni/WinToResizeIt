using UniversalResizeLib;

namespace UniversalResizeExample
{
    public partial class Form1 : UniversalResizeForm
    {
        public Form1()
        {
            InitializeComponent();
        }

        protected override bool EnableFontResize
        {
            get { return true; }
        }

        private void Form1_Load(object sender, System.EventArgs e)
        {

        }
    }
}
