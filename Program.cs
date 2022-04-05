using CommandLine;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Strike.Components;
using Strike.Components.Extensions;
using Strike.Components.Indexing;
using Strike.Components.Poco;
using Strike.Components.Util;
using Strike.Components.Util.ObjectToObjectMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static IndexTesting.Program;

namespace IndexTesting
{
    class Program
    {

        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }
            [Option('i', "index", Required = true, HelpText = "index directory")]
            public string IndexDirs { get; set; }
            
            [Option('q',"query", Separator = ' ',HelpText = "(query | %all | %enumerate-fields "
                + "| %count-fields "
                + "| %enumerate-terms field "
                + "| %script scriptFile "
                + "| %ids id [id ...] | %id-file file) (required, scriptFile may contain -q and -o)")]
           
            public IEnumerable<string> Query { get; set; }
           

            [Option("regex", Required = false, HelpText = "filter query by regex, syntax is field:/regex/")]
            public bool Regex { get; set; }

            [Option("fields", Required = false, HelpText = "fields to include in output (defaults to all)")]
            public IEnumerable<string> Fields { get; set; }
            [Option("sort-fields", Required = false, HelpText = "sort fields within document")]
            public bool SortFields { get; set; }

            [Option("query-limit", Required = false, Default= Int32.MaxValue, HelpText = "same as output-limit")]
            public int QueryLimit { get; set; }

            [Option("output-limit", Required = false, Default = Int32.MaxValue, HelpText = "max number of docs to output")]
            public int OutputLimit { get; set; }

            [Option("analyzer", Required = false, Default = "StandardAnalyzer", HelpText = "for query, (KeywordAnalyzer | StandardAnalyzer) (defaults to KeywordAnalyzer)")]
            public string Analyzer { get; set; }

            [Option("query-field", Required = false, HelpText = "default field for query")]
            public string QueryField { get; set; }

            [Option("show-id", Required = false, Default = true, HelpText = "show Lucene document id in results")]
            public bool ShowId { get; set; }

            [Option("show-score", Required = false, Default = true,HelpText = "show score in results")]
            public bool  ShowScore { get; set; }

            [Option("show-hits", Required = false, Default = true, HelpText = "show total hit count")]
            public bool ShowHits { get; set; }


        }


        static void Main(string[] args)
        {
            
            var options = CommandLine.Parser.Default.ParseArguments<Options>(args)
            .WithParsed(RunOptions)
            .WithNotParsed(HandleParseError);
            IndexReader r = null;
            LuceneQueryTool lqt = null;
           IEnumerable<string> runOptions =new List<string>();
            options.WithParsed(x => {
                var readers = x.IndexDirs.Split(',').Select(d => DirectoryReader.Open(FSDirectory.Open(d)));
                r = new MultiReader(readers.ToArray());
                lqt = new LuceneQueryTool(r)
                .SetQueryLimit(x.QueryLimit)
                .SetAnalyzer(x.Analyzer)
                .SetDefaultField(x.QueryField)
                .SetShowScore(x.ShowScore)
                .SetShowHits(x.ShowHits)
                .SetShowId(x.ShowId)
                .SetFormatter(new ConsoleTabularFormatter());
                runOptions = x.Query;
            });
            lqt.Run(runOptions.ToArray());

           

        }
        static void RunOptions(Options opts)
        {
            //handle options
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            //handle errors
        }
        public void CreateRadiologyIndex()
        {
            var radIndexOpt = new BaseIndexOptionBuilder()
             .IndexDirectory(@"c:\strike-data\indexes\radiology\")
             .DefaultField("report")
             .Language("en")
             .LuceneVersion(Lucene.Net.Util.LuceneVersion.LUCENE_48);


            ShardedIndex _radiologyIndex = new ShardedIndex(radIndexOpt.Build());




            Generator gen = new Generator();
            Random random = new Random();
            var mapper = new MapperOptimized();

            var p = gen.GetRandomPatient();
            p.Id = Guid.NewGuid().ToShortString();
            var v = gen.GetRandomVisit(p);
            var o = gen.GetRandomOrder(p, v);
            var report = gen.GetRandomReport(p, v, o);

            o.orderStatus = "C";
            var referring = gen.GetRandomProvider();
            o.orderStatusDt = random.NextDateTime(DateTime.Now.AddHours(-1), DateTime.Now.AddHours(3));
            mapper.Copy(p, o);
            mapper.Copy(v, o);


            //Expression<Func<patient, bool>> expr = p => p.account.Length < 5;

            //Func<patient, bool> deleg = expr.Compile();
            //// Invoke the method and print the output.
            //Console.WriteLine("deleg(4) = {0}", deleg(p));



            //Expression<Func<patient, object>> testPatient = x => x.account.ToString() + "test";
            //Func<patient, object> tp = testPatient.Compile()    ;
            //var id = tp.Invoke(p);






            Strike.Components.Poco.report rep = new Strike.Components.Poco.report();
            var patient_mapper = new Mapper<patient>()
                .Add(x => x.Id)
                .Add(x => x.mpi)
                .Add(x => x.organization)
                .Add(x => x.mrn)
                .Add(x => x.mrnIssuer)
                .Add("identities", x => x.identities.Select(x => $"{x.issuer}.{x.Id}.{x.type}").Join(","))
                .Add(x => x.account)
                .Add("name", x => $"{x.name.lastName}.{x.name.middleName}.{x.name.firstName}")
                .Add("lastName", x => x.name.lastName)
                .Add(x => x.height)
                .Add(x => x.race)
                .Add(x => x.weight);


            var visit_mapper = new Mapper<visit>()
               .Add(x => x.patientType)
               .Add(x => x.patientClass)
               .Add(x => x.patientLocation)
               .Add("referringMd", x => x.referringMd.ToIndexablestring())
               .Add(x => x.servicingFacility)
               .Add(x => x.visitNumber);

            var order_mapper = new Mapper<order>()
                .Add(x => x.accession)
                .Add(x => x.age)
                .Add("orderingMd", x => x.orderingMd.ToIndexablestring())
                .Add(x => x.orderingFacility)
                .Add("procedure", x => $"{x.procedureCode}.{x.procedureDesc}")
                .Add(x => x.reasonForStudy)
                .Add(x => x.bodyPart)
                .Add(x => x.subSpecialty)
                .Add(x => x.modality)

                .Add(x => x.rvu)
                .Add(x => x.studyUid);

            var result_mapper = new Mapper<report>()
                .Add("reportDt", x => x.reportDt.Value.ToString("yyyyMMddHHmm"))
                .Add("reportingMd", x => x.reportingMd.ToIndexablestring())
                .Add(x => x.reportStatus)
                .Add(x => x.text);







            patient_mapper.Map(p);
            order_mapper.Map(o);
            result_mapper.Map(report);

            var token_Fields = patient_mapper.tokenizedFields;
            token_Fields.AddRange(order_mapper.tokenizedFields);
            token_Fields.AddRange(result_mapper.tokenizedFields);

            var non_token_Fields = patient_mapper.nonTokenizedFields;
            non_token_Fields.AddRange(order_mapper.nonTokenizedFields);
            non_token_Fields.AddRange(result_mapper.nonTokenizedFields);


            _radiologyIndex.AddDocument("RadiologyReport", token_Fields, non_token_Fields, $"");
        }

    }

    public static class pocoExtensions
    {
        public static string ToIndexablestring(this provider x)
        {
            return $"{x.providerIdIssuer}.{x.Id}.{x.providerIdType}.{x.name.lastName}.{x.name.firstName}";
        }
    }
}
