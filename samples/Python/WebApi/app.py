import json
import os
import sys

from bson import json_util
from fastapi import FastAPI
from fastapi.responses import PlainTextResponse, Response
from pymongo import MongoClient
from scalar_fastapi import get_scalar_api_reference

port = int(os.environ.get("PORT", "8000"))
connection_string = os.environ.get("ConnectionStrings__TestMongoRS")

if not connection_string:
    print("MongoDB connection string is not configured. Set the ConnectionStrings__TestMongoRS environment variable.")
    sys.exit(1)

app = FastAPI(
    title="WebApi",
    version="1.0.0",
    docs_url=None,
    redoc_url=None,
)


@app.get("/")
async def scalar_docs():
    return get_scalar_api_reference(
        openapi_url=app.openapi_url,
        title=app.title,
    )


@app.get("/mongo/hello", summary="Run the MongoDB hello command")
async def mongo_hello():
    client: MongoClient = MongoClient(connection_string)
    try:
        database = client.get_database("admin")
        result = database.command("hello")
        return Response(
            content=json.dumps(result, default=json_util.default),
            media_type="application/json",
        )
    except Exception as err:
        print(f"Error executing MongoDB hello command: {err}")
        return Response(
            content=json.dumps({"error": "Failed to execute MongoDB hello command."}),
            status_code=500,
            media_type="application/json",
        )
    finally:
        client.close()


@app.get("/health", summary="Health check")
async def health():
    client: MongoClient = MongoClient(connection_string)
    try:
        client.get_database("admin").command("ping")
        return PlainTextResponse("Healthy")
    except Exception:
        return PlainTextResponse("Unhealthy", status_code=503)
    finally:
        client.close()


@app.get("/alive", summary="Liveness check")
async def alive():
    return PlainTextResponse("Alive")


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=port)
