# Outbox Pattern Implementation using Debezium and Google Protobuffers

This example shows that you can profit from [Debezium](https://debezium.io/) for an [Outbox Pattern implementation](https://debezium.io/documentation/reference/1.6/transformations/outbox-event-router.html) while also having the benefits of [Google Protobuffers](https://developers.google.com/protocol-buffers).

[Debezium](https://debezium.io/) is an open source project for [change data capture (CDC)](https://en.wikipedia.org/wiki/Change_data_capture).
This example has a few components to demonstrate the utilities.
- A dockerized Django web application that stores data in a MySQL database. This application writes the events to an outbox table in a protobuf binary serialized format.
- An [Apache Kafka](https://kafka.apache.org/) docker setup with a [Kafka Connect](https://docs.confluent.io/platform/current/connect/index.html) container that has the [Debezium MySQL connector](https://debezium.io/documentation/reference/connectors/mysql.html) installed. The Debezium connector listens to the MySQL binlog for a specific outbox table and pushes the changes to a Kafka topic
- A .NET Core React application with a PostgreSQL database that consumes this data.

Debezium

Requirements:<br>
- Just docker

To get the docker containers up and running:

    docker-compose up -d

To create the django tables in MySQL:

    docker-compose run --rm --no-deps python_app python manage.py migrate

To add some polls from admin page, create a superuser

    docker-compose run --rm --no-deps python_app python manage.py createsuperuser

Using the username/password you just generated, you can later visit http://localhost:8000/admin/polls/question/ and create some rows after setting up CDC.

Grant the required MySQL rights to django so that Debezium can do it's job. (This is done automatically using a volume in the `docker-compose.yml` file)
To do this yourself, go to Adminer UI at http://localhost:8080/. Login using:

    Server: mysql
    Username: root
    Password: pass
    Database: djangodb

After logging in, click "SQL command" and I executed this:

    GRANT SELECT, RELOAD, SHOW DATABASES, REPLICATION SLAVE, REPLICATION CLIENT ON *.* TO django@'%';

The next thing to do is set up Debezium by sending a cURL command to kafka connect.<br>
You can read about the Debezium MySQL connector configuration at https://debezium.io/documentation/reference/connectors/mysql.html#mysql-required-connector-configuration-properties

To send our binary Protobuffer data, we will use the same method as Avro configuration explained here: https://debezium.io/documentation/reference/transformations/outbox-event-router.html#avro-as-payload-format. This configuration is critical since we do not want the default configuration that will produce to Kafka using a Debezium JSON structure. This Debezium JSON structure would force our downstream consumers to also depend on that structure instead of a simple proto definition. That would also mean we would be losing [the serialization performance and data size efficiency of Protobuffers](https://dzone.com/articles/is-protobuf-5x-faster-than-json) since the data would be JSON serialized/deserialized. To achieve this, we are using the `ByteBufferConverter` class as our `value.converter` and we disable the schema information with `"value.converter.schemas.enable": "false"`

Open a new terminal, and use the curl command to register the Debezium MySQL connector. (You may need to escape your double-quotes on windows if you get a parsing error. Just use [PostMan](https://www.postman.com/) IMO). This will add a connector in our kafka-connect container to listen to database changes in our outbox table.

    curl -i -X POST -H "Accept:application/json" -H "Content-Type:application/json" localhost:8083/connectors/ --data-raw '{
        "name": "cdc-python-netcore-connector-outbox",
        "config": {
            "connector.class": "io.debezium.connector.mysql.MySqlConnector",
            "database.hostname": "mysql",
            "database.port": "3306",
            "database.user": "django",
            "database.password": "django",
            "database.server.name": "cdc-mysql",
            "database.history.kafka.topic": "cdc-test",
            "database.history.kafka.bootstrap.servers": "kafka:9092",
            "table.include.list": "djangodb.polls_outbox",
            "transforms": "outbox",
            "transforms.outbox.type" : "io.debezium.transforms.outbox.EventRouter",
            "value.converter": "io.debezium.converters.ByteBufferConverter",
            "value.converter.schemas.enable": "false",
            "value.converter.delegate.converter.type": "org.apache.kafka.connect.json.JsonConverter"
        }
    }'

Create new poll questions using the admin page at http://localhost:8000/admin/polls/question/add/ to trigger Kafka events.

To see if everything is running as expected go to our kafdrop container page at http://localhost:9000/
You should see the topics and produced messages.

To prepare a protobuf file between python and .net core, I wrote a proto file: `/proto/question.proto`. To install the protobuf compiler on a Mac without problems use:

    brew install protobuf

And run the following command inside the proto folder (I've already included the compiled output of the proto file in this repo).

    protoc question.proto --python_out=./output/python/ --csharp_out=./output/csharp/ --descriptor_set_out=question.desc

We now need an outbox table to implement the outbox pattern using Debezium. I created the Outbox model for this:

    class Outbox(models.Model):
        id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
        aggregatetype = models.CharField(max_length=255)
        aggregateid = models.CharField(max_length=255)
        event_type = models.CharField(max_length=255, db_column='type')
        payload = models.BinaryField()

You can read the [Debezium documentation](https://debezium.io/documentation/reference/configuration/outbox-event-router.html) for details. The `payload` column is special here since it will hold the binary serialized protobuf value and it will be passed transparently by Debezium to Kafka. This way, the downstream services can consume this payload without any dependency to Debezium.

To populate the outbox table, I used the `save_model` method of admin view:

    @transaction.atomic
    def save_model(self, request, obj, form, change):
        super().save_model(request, obj, form, change)
        self.create_outbox_record(obj)

    def create_outbox_record(self, obj):
        ts = Timestamp()
        ts.FromDatetime(obj.pub_date)
        proto = QuestionProto(
            id=obj.id,
            question_text=obj.question_text,
            pub_date=ts,
        )
        outbox = Outbox(
            aggregatetype='question',
            aggregateid=obj.id,
            event_type='question_created',
            payload=proto.SerializeToString(),
        )
        outbox.save()
        #outbox.delete()

The `SerializeToString()` method here is the google implementation from the python class that auto-generated using the proto file. It will give us the binary representation of our payload.

In the .NET client side, I created a standart .NET Core React startup project and built from there.
I added a single node Elasticsearch and Kibana to the docker-compose file for storage.

Since this is a web project, we need a background service to use as a Kafka consumer.

    public class QuestionConsumerService : BackgroundService
    {
        private readonly string topic;
        private readonly IConsumer<string, QuestionProto> kafkaConsumer;

        public QuestionConsumerService()
        {
            topic = "outbox.event.question";
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = "host.docker.internal:9092",
                GroupId = "hus-dotnet-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };
            kafkaConsumer = new ConsumerBuilder<string, QuestionProto>(consumerConfig)
                .SetValueDeserializer(new MyDeserializer())
                .SetErrorHandler((_, e) => Console.WriteLine($"Consumer Error at SetErrorHandler: {e.Reason}"))
                .Build();
        }
    ...

I used `host.docker.internal` to connect to Kafka since the .NET application was running in my machine and kafka was running in docker.
Just using `localhost:9092` wasn't enough since `ADVERTISED_LISTENERS=localhost` wouldn't working for zookeper, kafka-connect and kafdrop.
In my host machine, `host.docker.internal` resolves to the Hyper-V virtual machine that docker uses and in the internal containers, it resolves to the correct internal IP of docker VM which routes the port back to the kafka container. This way, [Everyone is Happy](https://www.youtube.com/watch?v=N1Np_Q1nmQ8).

Another gotcha was the deserialization of the payloads. At first I was using the example provided in the Confluent docs:

    kafkaConsumer = new ConsumerBuilder<string, QuestionProto>(consumerConfig)
                .SetValueDeserializer(new ProtobufDeserializer<QuestionProto>().AsSyncOverAsync())
                .SetErrorHandler((_, e) => Console.WriteLine($"Consumer Error at SetErrorHandler: {e.Reason}"))
                .Build();

But I got a `Confluent.Kafka.ConsumeException` saying `"Local: Value deserialization error"`. To better see the problem, you should take a look at the `InnerException`:

    System.IO.InvalidDataException: "Expecting message Value with Confluent Schema Registry framing. Magic byte was 8, expecting 0"

The `ProtobufDeserializer<T>` provided by Confluent wasn't working since it was expecting the payload to be serialized using the Confluent serializer and I was using the default Google implementation to serialize the `Question` protobuf values in my Python code:

    proto.SerializeToString()

So, I wrote a simple deserializer of my own that would work using the Google implementation provided in my generated C# protobuf class:

    public class MyDeserializer : IDeserializer<Question>
    {
        public Question Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
                return null;
            return Question.Parser.ParseFrom(data);
        }
    }

To store the received payloads in Elasticsearch, I created a simple Question class since the protobuf auto-generated class contained lots of utility code:

    public class Question
    {
        public int Id { get; set; }
        public string QuestionText { get; set; }
        public DateTime? PubDate { get; set; }
    }

Saving the payloads in Elasticsearch was pretty straightforward. No connection string was needed since the default is `localhost:9200`

    void SaveMessage(QuestionProto val)
    {
        var question = new Question
        {
            Id = val.Id,
            QuestionText = val.QuestionText,
            PubDate = val.PubDate?.ToDateTime()
        };
        var settings = new ConnectionSettings().DefaultIndex("question");
        var client = new ElasticClient(settings);
        client.IndexDocument(question);
    }

To display the latest saved payloads, I created a simple controller:

    [HttpGet]
    public IEnumerable<Question> Get()
    {
        var settings = new ConnectionSettings().DefaultIndex("question");
        var client = new ElasticClient(settings);
        var searchResponse = client.Search<Question>(s => s
            .Query(q => q.MatchAll())
            .Sort(q => q.Descending(question => question.Id))
            .Size(20)
        );
        return searchResponse.Documents;
    }

Then I modified the FetchData.js file to fetch from this Controller. The gotcha here was that the fields names in the received data in javascript side weren't matching those in the C# model.
The default serializer in .NET convert to PascalCase fields to camelCase.

    {questions.map(question =>
        <tr key={question.id}>
            <td>{question.id}</td>
            <td>{question.questionText}</td>
            <td>{question.pubDate}</td>
        </tr>
    )}
