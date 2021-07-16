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

    docker-compose run --rm --no-deps web python manage.py migrate
    docker-compose run --rm --no-deps web python manage.py makemigrations polls
    docker-compose run --rm --no-deps web python manage.py migrate polls

Add some polls from admin page

    docker-compose run --rm --no-deps web python manage.py createsuperuser

The default login and password for the admin site is admin:admin.
