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

import javax.net.ssl.SSLContext;
import javax.net.ssl.TrustManagerFactory;
import java.io.FileInputStream;
import java.security.KeyStore;
import java.security.cert.CertificateFactory;
import java.security.cert.X509Certificate;
import java.util.Collection;

@RestController
public class MongoController {

    private static final Logger log = LoggerFactory.getLogger(MongoController.class);
    private final String connectionString;
    private final SSLContext sslContext;

    public MongoController() {
        this.connectionString = System.getenv("ConnectionStrings__TestMongoRS");
        this.sslContext = buildSSLContext();
    }

    @GetMapping(value = "/mongo/hello", produces = MediaType.APPLICATION_JSON_VALUE)
    public ResponseEntity<String> mongoHello() {
        var settingsBuilder = MongoClientSettings.builder()
                .applyConnectionString(new ConnectionString(connectionString));
        if (sslContext != null) {
            settingsBuilder.applyToSslSettings(ssl -> ssl.context(sslContext));
        }
        try (MongoClient client = MongoClients.create(settingsBuilder.build())) {
            Document result = client.getDatabase("admin").runCommand(new Document("hello", 1));
            return ResponseEntity.ok(result.toJson());
        } catch (Exception e) {
            log.error("Error executing MongoDB hello command", e);
            return ResponseEntity.internalServerError()
                    .body("{\"error\": \"Failed to execute MongoDB hello command.\"}");
        }
    }

    // TODO: Fix the aspire trust configuration to produce a valid CA bundle and remove this code.
    private static SSLContext buildSSLContext() {
        String caBundlePath = System.getenv("MONGO_CA_BUNDLE_PATH");
        if (caBundlePath == null || caBundlePath.isEmpty()) {
            return null;
        }
        try {
            CertificateFactory cf = CertificateFactory.getInstance("X.509");
            Collection<? extends java.security.cert.Certificate> certs;
            try (FileInputStream fis = new FileInputStream(caBundlePath)) {
                certs = cf.generateCertificates(fis);
            }

            KeyStore trustStore = KeyStore.getInstance(KeyStore.getDefaultType());
            trustStore.load(null, null);
            int i = 0;
            for (var cert : certs) {
                trustStore.setCertificateEntry("ca-" + i++, cert);
            }

            TrustManagerFactory tmf = TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm());
            tmf.init(trustStore);

            SSLContext ctx = SSLContext.getInstance("TLS");
            ctx.init(null, tmf.getTrustManagers(), null);
            return ctx;
        } catch (Exception e) {
            log.warn("Failed to load CA bundle from {}: {}", caBundlePath, e.getMessage());
            return null;
        }
    }
}
