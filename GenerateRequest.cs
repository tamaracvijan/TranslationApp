using System;
using System.Collections.Generic;
using System.Text;

namespace TranslationApp
{
    public class GenerateRequest
    {
        public string model { get; set; }
        public string prompt { get; set; }
        public bool stream { get; set; } = false;
    }
}
