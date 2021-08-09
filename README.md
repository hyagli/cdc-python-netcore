Debezium is an open source project for change data capture (CDC).
This example has two components to demonstrate the utilities.
- A Django application with a MySQL database that saves data.
- A .NET Core application with a PostgreSQL database that consumes this data.

Debezium

Requirements
Just docker


Create your Django app:

    docker-compose run --rm --no-deps web django-admin startproject django_cdc .

Modify src/django_cdc/settings.py

    DATABASES = {
        'default': {
            'ENGINE': 'django.db.backends.mysql',
            'NAME': 'djangodb',
            'USER': 'django',
            'PASSWORD': 'django',
            'HOST': 'mysql',
            'PORT': 3306,
        }
    }

Run:

    docker-compose up -d

Run for default django tables:

    docker-compose run --rm --no-deps python_app python manage.py migrate
    docker-compose run --rm --no-deps python_app python manage.py makemigrations polls
    docker-compose run --rm --no-deps python_app python manage.py migrate polls

Add some polls from admin page

    docker-compose run --rm --no-deps python_app python manage.py createsuperuser

The default login and password for the admin site is admin:admin.

Grant the required MySQL rights to django so that CDC can do it's job:

    GRANT SELECT, RELOAD, SHOW DATABASES, REPLICATION SLAVE, REPLICATION CLIENT ON *.* TO django@'%';

Verify Debezium MySQL connector configuration. Read more about it at https://debezium.io/documentation/reference/connectors/mysql.html#mysql-required-connector-configuration-properties

    {
        "name": "cdc-python-netcore-connector",
        "config": {
            "connector.class": "io.debezium.connector.mysql.MySqlConnector",
            "tasks.max": "1",
            "database.hostname": "mysql",
            "database.port": "3306",
            "database.user": "django",
            "database.password": "django",
            "database.server.id": "123321",
            "database.server.name": "cdc-mysql",
            "database.include.list": "djangodb",
            "database.history.kafka.bootstrap.servers": "kafka:9092",
            "database.history.kafka.topic": "schema-changes.djangodb"
        }
    }

Open a new terminal, and use the curl command to register the Debezium MySQL connector. (You may need to escape your double-quotes on windows if you get a parsing error)

    curl -i -X POST -H "Accept:application/json" -H "Content-Type:application/json" localhost:8083/connectors/ -d '{"name":"cdc-python-netcore-connector","config":{"connector.class":"io.debezium.connector.mysql.MySqlConnector","tasks.max":"1","database.hostname":"mysql","database.port":"3306","database.user":"django","database.password":"django","database.server.id":"123321","database.server.name":"cdc-mysql","database.include.list":"djangodb","database.history.kafka.bootstrap.servers":"kafka:9092","database.history.kafka.topic":"schema-changes.djangodb"}}'

Create a new poll question using the admin page at http://localhost:8000/admin/polls/question/add/

Start a shell prompt in the kafka container:

    docker exec -it <kafka-container-name> bash

List all topics:

    bin/kafka-topics.sh --list --zookeeper zookeeper:2181

List messages in polls_question topic:

    bin/kafka-console-consumer.sh --bootstrap-server kafka:9092 --topic cdc-mysql.djangodb.polls_question --from-beginning

I created a protobuf file to have a well-defined type between python and .net core: `/proto/question.proto`. To compile the proto file, you can install the protobuf compiler using:

    brew install protobuf

And run the following command inside the proto folder (I've already included the compiled output of the proto file in the repo).

    protoc question.proto --python_out=./output/python/ --csharp_out=./output/cs/

We now need an outbox table to implement the cdc outbox pattern using Debezium. I created the Outbox model for this:

    class Outbox(models.Model):
        id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
        aggregatetype = models.CharField(max_length=255)
        aggregateid = models.CharField(max_length=255)
        event_type = models.CharField(max_length=255, db_column='type')
        payload = models.BinaryField()

You can read the Debezium documentation for details. The `payload` column is special here since it will hold the serialized protobuf value and it will be passed transparently by Debezium to Kafka.
