using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace IndexTesting
{
    public class LuceneQueryTool
    {

        IndexReader _reader;
        Analyzer _analyzer;

        private List<string> fieldNames;
        private HashSet<string> allFieldNames;
        private int? outputLimit;
        private string regexField;
        private Regex regex;
        private bool showId;
        private bool showHits;
        private bool showScore;
        private bool sortFields;
        private Analyzer analyzer;
        private string defaultField;
        
        private StreamWriter streamWriter;
        private int docsPrinted;
        private IFormatter formatter;


        LuceneQueryTool()
        {

        }

        public LuceneQueryTool(IndexReader reader, StreamWriter output)
        {
            Initialize(reader, output);
        }

        public LuceneQueryTool(IndexReader reader)
        {
            var sw = new StreamWriter(System.Console.OpenStandardOutput());
            sw.AutoFlush = true;
            Console.SetOut(sw);
            Initialize(reader, sw );
        }

        public void Initialize(IndexReader reader, StreamWriter output)
        {
            this._reader = reader;
            this.outputLimit = int.MaxValue;
            this._analyzer = new KeywordAnalyzer();
            this.fieldNames = new List<string>();
            this.streamWriter = output;
            allFieldNames = new HashSet<string>();
            foreach (var leaf in reader.Leaves)
            {
                foreach (FieldInfo fieldInfo in leaf.AtomicReader.FieldInfos)
                {
                    allFieldNames.Add(fieldInfo.Name);
                }
            }
            //this.formatter = Formatter.newInstance(Formatter.Format.MULTILINE, false);
        }

        public LuceneQueryTool SetFieldNames(List<string> fieldNames)
        {
            List<string> invalidFieldNames = new List<string>();
            foreach (string field in fieldNames)
            {
                if (!allFieldNames.Contains(field))
                {
                    invalidFieldNames.Add(field);
                }
            }
            if (invalidFieldNames.Count > 0)
            {
                throw new Exception("Invalid field names: " + invalidFieldNames);
            }
            fieldNames.AddRange(fieldNames);
            return this;
        }
        public LuceneQueryTool SetAnalyzer(string analyzerstring)
        {
            if ("KeywordAnalyzer".Equals(analyzerstring))
            {
                this.analyzer = new KeywordAnalyzer();
            }
            else if ("StandardAnalyzer".Equals(analyzerstring))
            {
                this.analyzer = new StandardAnalyzer(Lucene.Net.Util.LuceneVersion.LUCENE_48);
            }
            else
            {
                throw new Exception(
                        string.Format("Invalid analyzer {0}: {1}",
                                analyzerstring,
                                "Only KeywordAnalyzer and StandardAnalyzer currently supported"));
            }
            return this;
        }

        // same as outputLimit; for compatibility
        public LuceneQueryTool SetQueryLimit(int? queryLimit)
        {
            if (queryLimit == null) return this;
            this.outputLimit = queryLimit;
            return this;
        }

        public LuceneQueryTool SetOutputLimit(int? outputLimit)
        {
            if (outputLimit == null) return this;
            this.outputLimit = outputLimit;
            return this;
        }

        public LuceneQueryTool SetOutputStream(StreamWriter outstream)
        {
            this.streamWriter = outstream;
            return this;
        }

        public LuceneQueryTool SetRegex(string regexField, Regex regex)
        {
            if (!allFieldNames.Contains(regexField))
            {
                throw new Exception("Invalid field name: " + regexField);
            }
            if (fieldNames.Count > 0 && !fieldNames.Contains(regexField))
            {
                throw new Exception("Attempted to apply regex to field not in results: " + regexField);
            }
            this.regexField = regexField;
            this.regex = regex;
            return this;
        }

        public LuceneQueryTool SetShowId(bool showId)
        {
            this.showId = showId;
            return this;
        }

        public LuceneQueryTool SetSortFields(bool sortFields)
        {
            this.sortFields = sortFields;
            return this;
        }

        public LuceneQueryTool SetShowHits(bool showHits)
        {
            this.showHits = showHits;
            return this;
        }

        public LuceneQueryTool SetShowScore(bool showScore)
        {
            this.showScore = showScore;
            return this;
        }

        public LuceneQueryTool SetDefaultField(string defaultField)
        {
            if (string.IsNullOrEmpty(defaultField)) return this;
            if (!allFieldNames.Contains(defaultField))
            {
                throw new Exception("Invalid field name: " + defaultField);
            }
            this.defaultField = defaultField;
            return this;
        }

        public LuceneQueryTool SetFormatter(IFormatter formatter)
        {
            this.formatter = formatter;
            return this;
        }


        private void DumpIds(IEnumerable<string> ids)
        {
            docsPrinted = 0;

            foreach (var id in ids)
            {
                var docId = Convert.ToInt32(id);
                Document doc = _reader.Document(docId);
                PrintDocument(doc, docId, 1.0f);
            }

        }
        private void PrintDocument(Document doc, int id, float score)
        {
            MultiValueDictionary<string, string> data = new MultiValueDictionary<string, string>();
            List<string> orderedFieldNames = new List<string>();
            if (showId)
            {
                orderedFieldNames.Add("<id>");
                data.Add("<id>", id.ToString());
            }
            if (showScore)
            {
                orderedFieldNames.Add("<score>");
                data.Add("<score>", score.ToString());
            }
            orderedFieldNames.AddRange(fieldNames);

            HashSet<string> setFieldNames = new HashSet<string>();
            if (fieldNames.Count == 0)
            {
                foreach (IIndexableField f in doc.Fields)
                {
                    if (!setFieldNames.Contains(f.Name))
                    {
                        orderedFieldNames.Add(f.Name);
                    }
                    setFieldNames.Add(f.Name);
                }
            }
            else
            {
                setFieldNames.UnionWith(fieldNames);
            }
            if (sortFields)
            {
                orderedFieldNames.Sort();
            }

            foreach (IIndexableField f in doc.Fields)
            {
                if (setFieldNames.Contains(f.Name))
                {
                    if (f.GetStringValue() != null)
                    {
                        data.Add(f.Name, f.GetStringValue());
                    }
                    else if (f.GetBinaryValue() != null)
                    {
                        data.Add(f.Name, f.GetBinaryValue().Utf8ToString());
                    }
                    else
                    {
                        data.Add(f.Name, "null");
                    }
                }
            }

            if (docsPrinted == 0 && formatter.Format == Format.TABULAR && !formatter.SupressNames)
            {
                streamWriter.WriteLine(string.Join('\t', orderedFieldNames));
            }

            string formatted = formatter.FormatData(orderedFieldNames, data);
            if (!formatted.isEmpty())
            {
                if (docsPrinted > 0 && formatter.Format == Format.MULTILINE)
                {
                    streamWriter.Write('\n');
                }
                streamWriter.WriteLine(formatted);
                ++docsPrinted;
            }
        }
        public void EnumerateTerms(string field)
        {
            if (!allFieldNames.Contains(field))
            {
                throw new Exception("Invalid field name: " + field);
            }
            var leaves = _reader.Leaves;

            bool unindexedField = true;
            Dictionary<string, int> termCountMap = new Dictionary<string, int>();
            foreach (AtomicReaderContext leaf in leaves)
            {
                Terms terms = leaf.AtomicReader.GetTerms(field);
                if (terms == null)
                {
                    continue;
                }
                unindexedField = false;


                foreach (var term in terms)
                {
                    var bytesRef = term.Term;
                    var termStr = bytesRef.Utf8ToString();
                    if (termCountMap.ContainsKey(termStr))
                    {
                        termCountMap[termStr] = term.DocFreq + termCountMap[termStr];
                    }
                    else
                    {
                        termCountMap.Add(termStr, term.DocFreq);
                    }
                }
            }
            if (unindexedField)
            {
                throw new Exception("Unindexed field: " + field);
            }
            foreach (var entry in termCountMap)
            {
                streamWriter.WriteLine(entry.Key + " (" + entry.Value + ")");
            }
        }

        private void CountFields()
        {
            foreach (string field in allFieldNames)
            {
                var leaves = _reader.Leaves;
                var fieldCounts = new Dictionary<string, int>();
                int count = 0;
                foreach (AtomicReaderContext leaf in leaves)
                {
                    Terms terms = leaf.AtomicReader.GetTerms(field);
                    if (terms == null)
                    {
                        continue;
                    }
                    count += terms.DocCount;
                }
                fieldCounts[field] = count;
                foreach (var entry in fieldCounts)
                {
                    streamWriter.WriteLine(entry.Key + ": " + entry.Value);
                }
            }
        }


        public void Run(string[] queryOpts)
        {
            Run(queryOpts, streamWriter);
        }
        public void Run(string[] queryOpts, StreamWriter outStream)
        {
            //    if (formatter.getFormat() == Formatter.Format.TABULAR && fieldNames.isEmpty()) {
            //        // Unlike a SQL result set, Lucene docs from a single query (or %all) may
            //        // have different fields, so a tabular format won't make sense unless we
            //        // know the exact fields beforehand.
            //        throw new RuntimeException("--tabular requires --fields to be passed");
            //}

            if (sortFields)
                fieldNames.Sort();
            string opt = queryOpts[0];
            if ("%ids".Equals(opt))
            {
                List<string> ids = new List<string>(queryOpts[1..queryOpts.Length]);
                DumpIds(ids);
            }
            else if ("%id-file".Equals(opt))
            {
                var iterator = new StreamReader(queryOpts[1]).ReadToEnd().Split(Environment.NewLine);
                DumpIds(iterator);
            }
            else if ("%all".Equals(opt))
            {
                RunQuery(null, outStream);
            }
            else if ("%enumerate-fields".Equals(opt))
            {
                foreach(string fieldName in allFieldNames)
                {
                        streamWriter.WriteLine(fieldName);
                }
            }
            else if ("%count-fields".Equals(opt))
            {
                CountFields();
            }
            else if ("%enumerate-terms".Equals(opt))
            {
                if (queryOpts.Length != 2)
                {
                    throw new Exception("%enumerate-terms requires exactly one field.");
                }
                EnumerateTerms(queryOpts[1]);
            }
            //else if ("%script".Equals(opt))
            //{
            //    if (queryOpts.Length != 2)
            //    {
            //        throw new Exception("%script requires exactly one arg.");
            //    }
            //    runScript(queryOpts[1]);
            //}
            else
            {
                RunQuery(queryOpts[0], outStream);
            }
        }

        HashSet<string> fieldSet = null;
        IndexSearcher searcher = null;
        private void RunQuery(string querystring, StreamWriter outStream)
        {
            searcher = new IndexSearcher(_reader);
            docsPrinted = 0;
            Query query;
            if (querystring == null)
            {
                query = new MatchAllDocsQuery();
            }
            else
            {
                if (!querystring.Contains(":") && defaultField == null)
                {
                    throw new Exception("query has no ':' and no query-field defined");
                }
                QueryParser queryParser = new QueryParser(Lucene.Net.Util.LuceneVersion.LUCENE_48, defaultField, analyzer);
                queryParser.LowercaseExpandedTerms = false;
                query = queryParser.Parse(querystring).Rewrite(_reader);
                HashSet<Term> terms = new HashSet<Term>();
                var weight = query.CreateWeight(searcher);
                query.ExtractTerms(terms);
                List<string> invalidFieldNames = new List<string>();
                foreach (Term term in terms)
                {
                    if (!allFieldNames.Contains(term.Field))
                    {
                        invalidFieldNames.Add(term.Field);
                    }
                }
                if (!invalidFieldNames.isEmpty())
                {
                    throw new Exception("Invalid field names: " + invalidFieldNames);
                }
            }
            fieldSet = new HashSet<string>(fieldNames);
            var _collector = Collector.NewAnonymous(this.setScorer, this.Collect, this.SetNextReader, () => true);
            searcher.Search(query, _collector);
            if (showHits)
            {
                streamWriter.WriteLine("totalHits: " + totalHits);
                streamWriter.WriteLine();
            }



        }

        private long totalHits;
        private int docBase;
        private Scorer scorer;



        public void SetNextReader(AtomicReaderContext context)
        {
            docBase = context.DocBase;
        }

        public void Collect(int id)
        {
            totalHits++;
            if (docsPrinted >= outputLimit)
            {
                return;
            }

            id += docBase;
            Document doc = fieldSet.Count == 0 ? searcher.Doc(id) : searcher.Doc(id, fieldSet);
            bool passedFilter = regexField == null;
            if (regexField != null)
            {
                string value = doc.Get(regexField);
                if (value != null && regex.Match(value).Success)
                {
                    passedFilter = true;
                }
            }
            if (passedFilter)
            {
                float score = scorer.GetScore();
                PrintDocument(doc, id, score);
            }
        }
        public void setScorer(Scorer _scorer)
        {
            scorer = _scorer;
        }

    }



}
