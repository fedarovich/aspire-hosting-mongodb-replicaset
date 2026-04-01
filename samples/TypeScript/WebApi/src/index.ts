import express from 'express';
import { MongoClient, MongoClientOptions } from 'mongodb';
import { apiReference } from '@scalar/express-api-reference';
import tls from 'node:tls';

const port = parseInt(process.env['PORT'] ?? '3000');
const connectionString = process.env['ConnectionStrings__TestMongoRS'];

if (!connectionString) {
    console.error('MongoDB connection string is not configured. Set the ConnectionStrings__TestMongoRS environment variable.');
    process.exit(1);
}

const mongoClientOptions: MongoClientOptions = {};

const openApiDocument = {
    openapi: '3.0.0',
    info: {
        title: 'WebApi',
        version: '1.0.0',
    },
    paths: {
        '/mongo/hello': {
            get: {
                summary: 'Run the MongoDB hello command',
                operationId: 'getMongoHello',
                responses: {
                    '200': {
                        description: 'MongoDB server info',
                        content: {
                            'application/json': {
                                schema: { type: 'object' },
                            },
                        },
                    },
                },
            },
        },
        '/health': {
            get: {
                summary: 'Health check',
                operationId: 'health',
                responses: {
                    '200': { description: 'Healthy' },
                    '503': { description: 'Unhealthy' },
                },
            },
        },
        '/alive': {
            get: {
                summary: 'Liveness check',
                operationId: 'alive',
                responses: {
                    '200': { description: 'Alive' },
                },
            },
        },
    },
};

const app = express();

app.get('/mongo/hello', async (_req, res) => {
    const client = new MongoClient(connectionString, mongoClientOptions);
    try {
        await client.connect();
        const database = client.db('admin');
        const result = await database.command({ hello: 1 });
        res.json(result);
    } catch (err) {
        console.error('Error executing MongoDB hello command:', err);
        res.status(500).json({ error: 'Failed to execute MongoDB hello command.' });
    } finally {
        client.close();
    }
});

app.get('/health', async (_req, res) => {
    const client = new MongoClient(connectionString, mongoClientOptions);
    try {
        await client.connect();
        await client.db('admin').command({ ping: 1 });
        res.status(200).send('Healthy');
    } catch {
        res.status(503).send('Unhealthy');
    } finally {
        client.close();
    }
});

app.get('/alive', (_req, res) => {
    res.status(200).send('Alive');
});

app.get('/openapi.json', (_req, res) => {
    res.json(openApiDocument);
});

app.use('/', apiReference({ url: '/openapi.json' }));

app.listen(port, () => {
    console.log(`WebApi is listening on port ${port}`);
});
