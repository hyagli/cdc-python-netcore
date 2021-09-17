package hus.springkafkakotlin

import org.springframework.boot.autoconfigure.SpringBootApplication
import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.boot.runApplication
import org.springframework.kafka.annotation.EnableKafka


@SpringBootApplication
@EnableKafka
@EnableConfigurationProperties(CustomConfigs::class)
class SpringKafkaKotlinApplication

fun main(args: Array<String>) {
	runApplication<SpringKafkaKotlinApplication>(*args)
}
