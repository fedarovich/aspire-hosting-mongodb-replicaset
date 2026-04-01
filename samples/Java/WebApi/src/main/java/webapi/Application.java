package webapi;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;

@SpringBootApplication
public class Application {

    public static void main(String[] args) {
        String connectionString = System.getenv("ConnectionStrings__TestMongoRS");
        if (connectionString == null || connectionString.isEmpty()) {
            System.err.println("MongoDB connection string is not configured. Set the ConnectionStrings__TestMongoRS environment variable.");
            System.exit(1);
        }
        SpringApplication.run(Application.class, args);
    }
}
