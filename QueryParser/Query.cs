using System.Collections.Generic;
using Newtonsoft.Json;

namespace QueryParser
{
    public class Query
    {
        public QueryExpression Where;
        public (FieldToken From, QueryExpression Filter, bool Index) From;
        public List<(QueryExpression Expression, FieldToken Alias)> Select;
        public List<(FieldToken Field, bool Ascending)> OrderBy;
        public string QueryText;

        public void ToJsonAst(JsonWriter writer)
        {
            writer.WriteStartObject();
            if (Select != null)
            {
                writer.WritePropertyName("Select");
                writer.WriteStartArray();

                foreach (var field in Select)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Expression");
                    field.Expression.ToJsonAst(QueryText, writer);
                    if (field.Alias != null)
                    {
                        writer.WritePropertyName("Alias");
                        QueryExpression.WriteValue(QueryText, writer, field.Alias.TokenStart, field.Alias.TokenLength,
                            field.Alias.EscapeChars);
                    }
                    writer.WriteEndObject();
                }
                
                writer.WriteEndArray();
            }
            writer.WritePropertyName("From");
            writer.WriteStartObject();
            writer.WritePropertyName("Index");
            writer.WriteValue(From.Index);
            writer.WritePropertyName("Source");
            QueryExpression.WriteValue(QueryText, writer, From.From.TokenStart, From.From.TokenLength,
                      From.From.EscapeChars);
            if(From.Filter != null)
            {
                writer.WritePropertyName("Filter");
                From.Filter.ToJsonAst(QueryText, writer);
            }
            writer.WriteEndObject();
            if (Where != null)
            {
                writer.WritePropertyName("Where");
                Where.ToJsonAst(QueryText, writer);
            }
            if (OrderBy != null)
            {
                writer.WritePropertyName("OrderBy");
                writer.WriteStartArray();
                foreach (var field in OrderBy)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Field");
                    QueryExpression.WriteValue(QueryText, writer, field.Field.TokenStart, field.Field.TokenLength,
                        field.Field.EscapeChars);
                    writer.WritePropertyName("Ascending");
                    writer.WriteValue(field.Ascending);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
    }
}