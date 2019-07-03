using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenericPopulator.Attributes
{
   [AttributeUsage(AttributeTargets.Property)]
   public class DatabaseProperty : Attribute
   {
      private string _columnName;
      public string ColumnName
      {
         get { return _columnName; }
         set { _columnName = value; }
      }
   }
}
