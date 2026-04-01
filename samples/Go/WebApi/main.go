package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"

	"go.mongodb.org/mongo-driver/v2/bson"
	"go.mongodb.org/mongo-driver/v2/mongo"
	"go.mongodb.org/mongo-driver/v2/mongo/options"
)

var connectionString string

func main() {
	port := os.Getenv("PORT")
	if port == "" {
		port = "8080"
	}

	connectionString = os.Getenv("ConnectionStrings__TestMongoRS")
	if connectionString == "" {
		log.Fatal("MongoDB connection string is not configured. Set the ConnectionStrings__TestMongoRS environment variable.")
	}

	http.HandleFunc("GET /mongo/hello", mongoHelloHandler)
	http.HandleFunc("GET /health", healthHandler)
	http.HandleFunc("GET /alive", aliveHandler)
	http.HandleFunc("GET /openapi.json", openAPIHandler)
	http.HandleFunc("GET /", scalarHandler)

	log.Printf("WebApi is listening on port %s", port)
	log.Fatal(http.ListenAndServe(":"+port, nil))
}

func mongoHelloHandler(w http.ResponseWriter, r *http.Request) {
	client, err := mongo.Connect(options.Client().ApplyURI(connectionString))
	if err != nil {
		log.Printf("Error connecting to MongoDB: %v", err)
		writeJSONError(w, http.StatusInternalServerError, "Failed to execute MongoDB hello command.")
		return
	}
	defer client.Disconnect(r.Context())

	var result bson.M
	err = client.Database("admin").RunCommand(r.Context(), bson.D{{Key: "hello", Value: 1}}).Decode(&result)
	if err != nil {
		log.Printf("Error executing MongoDB hello command: %v", err)
		writeJSONError(w, http.StatusInternalServerError, "Failed to execute MongoDB hello command.")
		return
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(result)
}

func healthHandler(w http.ResponseWriter, r *http.Request) {
	client, err := mongo.Connect(options.Client().ApplyURI(connectionString))
	if err != nil {
		w.WriteHeader(http.StatusServiceUnavailable)
		fmt.Fprint(w, "Unhealthy")
		return
	}
	defer client.Disconnect(r.Context())

	err = client.Database("admin").RunCommand(r.Context(), bson.D{{Key: "ping", Value: 1}}).Err()
	if err != nil {
		w.WriteHeader(http.StatusServiceUnavailable)
		fmt.Fprint(w, "Unhealthy")
		return
	}

	fmt.Fprint(w, "Healthy")
}

func aliveHandler(w http.ResponseWriter, _ *http.Request) {
	fmt.Fprint(w, "Alive")
}

func openAPIHandler(w http.ResponseWriter, _ *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.Write([]byte(openAPIDocument))
}

func scalarHandler(w http.ResponseWriter, _ *http.Request) {
	w.Header().Set("Content-Type", "text/html")
	w.Write([]byte(scalarHTML))
}

func writeJSONError(w http.ResponseWriter, status int, message string) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(map[string]string{"error": message})
}

const openAPIDocument = `{
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
}`

const scalarHTML = `<!DOCTYPE html>
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
</html>`
