using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace net.PeterCashel.DependencyResolver
{
    class Utilities
    {


        public static void CopyStream(Stream input, Stream output)
        {
            byte[] b = new byte[32768];
            int r;
            while ((r = input.Read(b, 0, b.Length)) > 0)
                output.Write(b, 0, r);
        }
    }
}
