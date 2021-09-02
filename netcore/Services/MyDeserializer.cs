using Com.Hus.Cdc;
using Confluent.Kafka;
using System;

namespace netCoreClient.Services
{
    // Needed this class since Confluent deserializer needs the data to be serialized using confluent serializer
    public class MyDeserializer : IDeserializer<Question>
    {
        public Question Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
                return null;
            return Question.Parser.ParseFrom(data);
        }
    }
}
