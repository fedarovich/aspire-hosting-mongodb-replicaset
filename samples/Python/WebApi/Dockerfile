FROM python:3.13-slim AS base
WORKDIR /app
EXPOSE 8000

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

ENV PORT=8000
ENTRYPOINT ["python", "app.py"]
