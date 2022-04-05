using ConsoleTables;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IndexTesting
{

    public enum Format
    {
        MULTILINE,
        TABULAR,
        JSON,
        JSON_PRETTY,

    }

       


    public interface IFormatter
    {
       
        public bool SupressNames { get; set; }
        public Format Format { get; set; }

        public string FormatData(List<string> header, MultiValueDictionary<string,string> data);

    }

    public static class stringExtensions
    {
        public static bool isEmpty(this string val)
        {
            return string.IsNullOrEmpty(val);
        }
        public static bool isEmpty(this List<string> val)
        {
            return val.Count==0;
        }
        public static bool isEmpty(this HashSet<string> val)
        {
            return val.Count == 0;
        }
        public static void WriteLn(this System.IO.Stream outStream, string data)
        {
            outStream.Write(Encoding.ASCII.GetBytes(data));
            outStream.Write(Encoding.ASCII.GetBytes("\n"));

        }

    }


    public class ConsoleTabularFormatter : IFormatter
    {
        public bool SupressNames { get; set; }
        public Format Format { get ; set ; }

        public string FormatData(List<string> header, MultiValueDictionary<string, string> data)
        {
            var table = new ConsoleTable();
            table.AddColumn(header);
            table.AddRow(data.Values.Select(x=>string.Join(',',x.Select(y=>y).ToArray())).ToArray());
            return table.ToString();
            //header.ForEach(x => table.AddColumn(x));
            //foreach (var entry in data)
            //    table.AddRow(entry.Key, entry.Value);


        }
    }
}
