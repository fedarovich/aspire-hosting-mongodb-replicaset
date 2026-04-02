package webapi;

import com.mongodb.ConnectionString;
import com.mongodb.MongoClientSettings;
import com.mongodb.client.MongoClient;
import com.mongodb.client.MongoClients;
import org.bson.Document;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
public class MongoController {

    private static final Logger log = LoggerFactory.getLogger(MongoController.class);
    private final String connectionString;

    public MongoController() {
        this.connectionString = System.getenv("ConnectionStrings__TestMongoRS");
    }

    @GetMapping(value = "/mongo/hello", produces = MediaType.APPLICATION_JSON_VALUE)
    public ResponseEntity<String> mongoHello() {
        var settingsBuilder = MongoClientSettings.builder()
                .applyConnectionString(new ConnectionString(connectionString));

        try (MongoClient client = MongoClients.create(settingsBuilder.build())) {
            Document result = client.getDatabase("admin").runCommand(new Document("hello", 1));
            return ResponseEntity.ok(result.toJson());
        } catch (Exception e) {
            log.error("Error executing MongoDB hello command", e);
            return ResponseEntity.internalServerError()
                    .body("{\"error\": \"Failed to execute MongoDB hello command.\"}");
        }
    }
}
