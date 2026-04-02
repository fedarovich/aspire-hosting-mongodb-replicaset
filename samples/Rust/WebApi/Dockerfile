FROM rust:latest AS build
WORKDIR /src
COPY Cargo.toml Cargo.lock* ./
# Create dummy main to cache dependency build
RUN mkdir src && echo "fn main() {}" > src/main.rs && cargo build --release && rm -rf src
COPY src ./src
RUN touch src/main.rs && cargo build --release

FROM debian:bookworm-slim AS final
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libssl3 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /src/target/release/webapi .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["./webapi"]
