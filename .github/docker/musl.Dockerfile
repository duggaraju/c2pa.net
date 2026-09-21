# syntax=docker/dockerfile:1
ARG RUST_VERSION=1.88.0
FROM rust:${RUST_VERSION}-alpine3.22 AS build

RUN apk add --no-cache build-base cmake perl pkgconf
WORKDIR /src
COPY c2pa-rs/ .

# Build a cdylib against Alpine's libc and unwind support, not the Ubuntu host's.
ENV RUSTFLAGS="-A unused-imports -C target-feature=-crt-static"
RUN cargo build --locked --release --target x86_64-unknown-linux-musl \
    --no-default-features --features rust_native_crypto,http,add_thumbnails -p c2pa-c-ffi

# TODO: Add linux-musl-arm64 with an ARM64 build and native package-consumer gate.
FROM scratch AS artifact
COPY --from=build /src/target/x86_64-unknown-linux-musl/release/libc2pa_c.so /
