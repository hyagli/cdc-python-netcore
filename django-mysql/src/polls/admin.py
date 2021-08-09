from django.contrib import admin
from django.db import transaction
from google.protobuf.timestamp_pb2 import Timestamp

from .models import Choice, Question, Outbox
from proto.question_pb2 import Question as QuestionProto


class ChoiceInline(admin.TabularInline):
    model = Choice
    extra = 3


class QuestionAdmin(admin.ModelAdmin):
    fieldsets = [
        (None,               {'fields': ['question_text']}),
        ('Date information', {'fields': ['pub_date'],
                              'classes': ['collapse']}),
    ]
    inlines = [ChoiceInline]
    list_display = ('question_text', 'pub_date', 'was_published_recently')
    list_filter = ['pub_date']
    search_fields = ['question_text']

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


admin.site.register(Question, QuestionAdmin)
