using System.Collections.Generic;
using Newtonsoft.Json;

namespace QueryParser
{
    public class Query
    {
        public QueryExpression Where;
        public (FieldToken From, QueryExpression Filter, bool Index) From;
        public List<(FieldToken Field, FieldToken Alias)> Select;
        public List<(FieldToken Field, bool Ascending)> OrderBy;
        public string QueryText;

        public void ToJsonAst(JsonWriter writer)
        {
            if (Select != null)
            {
                writer.WritePropertyName("Select");
                writer.WriteStartArray();

                foreach (var field in Select)
                {
                    if (field.Alias == null)
                    {
                        QueryExpression.WriteValue(QueryText, writer, field.Field.TokenStart, field.Field.TokenLength,
                            field.Field.EscapeChars);
                        continue;
                    }
                    writer.WriteStartObject();
                    writer.WritePropertyName("Field");
                    QueryExpression.WriteValue(QueryText, writer, field.Field.TokenStart, field.Field.TokenLength,
                        field.Field.EscapeChars);
                    writer.WritePropertyName("Alias");
                    QueryExpression.WriteValue(QueryText, writer, field.Alias.TokenStart, field.Alias.TokenLength,
                        field.Alias.EscapeChars);
                    writer.WriteEndObject();
                }
                
                writer.WriteEndArray();
            }

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
        }
    }
}