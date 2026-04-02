use actix_web::{get, web, App, HttpResponse, HttpServer};
use mongodb::{bson::doc, Client};
use std::env;

struct AppState {
    connection_string: String,
}

#[get("/mongo/hello")]
async fn mongo_hello(data: web::Data<AppState>) -> HttpResponse {
    let client = match Client::with_uri_str(&data.connection_string).await {
        Ok(c) => c,
        Err(e) => {
            eprintln!("Error connecting to MongoDB: {e}");
            return HttpResponse::InternalServerError()
                .json(serde_json::json!({"error": "Failed to execute MongoDB hello command."}));
        }
    };
    let db = client.database("admin");
    match db.run_command(doc! { "hello": 1 }, None).await {
        Ok(result) => HttpResponse::Ok().json(result),
        Err(e) => {
            eprintln!("Error executing MongoDB hello command: {e}");
            HttpResponse::InternalServerError()
                .json(serde_json::json!({"error": "Failed to execute MongoDB hello command."}))
        }
    }
}

#[get("/health")]
async fn health(data: web::Data<AppState>) -> HttpResponse {
    let client = match Client::with_uri_str(&data.connection_string).await {
        Ok(c) => c,
        Err(_) => return HttpResponse::ServiceUnavailable().body("Unhealthy"),
    };
    match client.database("admin").run_command(doc! { "ping": 1 }, None).await {
        Ok(_) => HttpResponse::Ok().body("Healthy"),
        Err(_) => HttpResponse::ServiceUnavailable().body("Unhealthy"),
    }
}

#[get("/alive")]
async fn alive() -> HttpResponse {
    HttpResponse::Ok().body("Alive")
}

#[get("/openapi.json")]
async fn openapi() -> HttpResponse {
    HttpResponse::Ok()
        .content_type("application/json")
        .body(OPENAPI_DOCUMENT)
}

#[get("/")]
async fn scalar() -> HttpResponse {
    HttpResponse::Ok()
        .content_type("text/html")
        .body(SCALAR_HTML)
}

#[tokio::main]
async fn main() -> std::io::Result<()> {
    let port: u16 = env::var("PORT")
        .unwrap_or_else(|_| "8080".to_string())
        .parse()
        .expect("PORT must be a valid number");

    let connection_string = env::var("ConnectionStrings__TestMongoRS").expect(
        "MongoDB connection string is not configured. Set the ConnectionStrings__TestMongoRS environment variable.",
    );

    let data = web::Data::new(AppState { connection_string });

    println!("WebApi is listening on port {port}");
    HttpServer::new(move || {
        App::new()
            .app_data(data.clone())
            .service(mongo_hello)
            .service(health)
            .service(alive)
            .service(openapi)
            .service(scalar)
    })
    .bind(("0.0.0.0", port))?
    .run()
    .await
}

const OPENAPI_DOCUMENT: &str = r#"{
  "openapi": "3.0.0",
  "info": {
    "title": "WebApi",
    "version": "1.0.0"
  },
  "paths": {
    "/mongo/hello": {
      "get": {
        "summary": "Run the MongoDB hello command",
        "operationId": "getMongoHello",
        "responses": {
          "200": {
            "description": "MongoDB server info",
            "content": {
              "application/json": {
                "schema": { "type": "object" }
              }
            }
          }
        }
      }
    },
    "/health": {
      "get": {
        "summary": "Health check",
        "operationId": "health",
        "responses": {
          "200": { "description": "Healthy" },
          "503": { "description": "Unhealthy" }
        }
      }
    },
    "/alive": {
      "get": {
        "summary": "Liveness check",
        "operationId": "alive",
        "responses": {
          "200": { "description": "Alive" }
        }
      }
    }
  }
}"#;

const SCALAR_HTML: &str = r#"<!DOCTYPE html>
<html>
<head>
  <title>WebApi</title>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
</head>
<body>
  <script id="api-reference" data-url="/openapi.json"></script>
  <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
</body>
</html>"#;
