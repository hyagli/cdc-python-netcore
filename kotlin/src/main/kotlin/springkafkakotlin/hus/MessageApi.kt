package hus.springkafkakotlin

import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController

@RestController
@RequestMapping("/api")
class MessageApi(private val kotlinProducer: KotlinProducer) {
  @GetMapping("/questions")
  fun get(@RequestBody message: String) : String {
    return "list of questions"
  }
}
