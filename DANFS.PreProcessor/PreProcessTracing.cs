using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DANFS.PreProcessor
{
    class PreProcessTracing : TextWriterTraceListener
    {
        public PreProcessTracing(string fileName) : base(new FileStream(fileName, FileMode.OpenOrCreate))
        {

        }
    }
}
