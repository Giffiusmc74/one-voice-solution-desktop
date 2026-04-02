using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1.src
{
    public static class MacroListChangeNotifier
    {
        public delegate void ListChangedEventHandler();
        public static event ListChangedEventHandler ListChanged;

        public static void NotifyListChanged()
        {
            ListChanged?.Invoke();
        }
    }
}
