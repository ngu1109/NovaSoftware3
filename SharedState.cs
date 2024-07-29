using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace NovaSoftware
{
    public static class SharedState
    {
        public static StorageFile CurrentSalesFile { get; set; }
        public static StorageFile CurrentStockFile { get; set; }
    }
}
