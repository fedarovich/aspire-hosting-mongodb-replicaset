// Aspire TypeScript AppHost
// For more information, see: https://aspire.dev

import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const mongoServer = await builder.addMongoDB('TestMongo', { port: 27017 });

await mongoServer.addDatabase('TestMongoDB');

const mongoRS = await builder.addMongoDBReplicaSet('TestMongoRS')
    .withMember(mongoServer)
    .withDbGate();

// Add sample services that use the replica set.

const dotNetLocal = await builder.addProject('DotNet-Local', '../../CSharp/WebApi/WebApi.csproj', 'Container (Dockerfile)')
    .withHttpEndpoint({ targetPort: 8080 })
    .withHttpsEndpoint({ targetPort: 8081 })
    .withReference(mongoRS);

const dotNetContainer = await builder.addDockerfileForProject('DotNet-Container', '../../CSharp/WebApi/WebApi.csproj')
    .withReference(mongoRS);

const nodeLocal = await builder.addJavaScriptApp('NodeJS-Local', '../WebApi')
    .withHttpEndpoint({ targetPort: 3000 })
    .withReference(mongoRS);

const nodeContainer = await builder.addDockerfile('NodeJS-Container', '../WebApi')
    .withHttpEndpoint({ targetPort: 3000 })
    .withReference(mongoRS);

const pythonContainer = await builder.addDockerfile('Python-Container', '../../Python/WebApi')
    .withHttpEndpoint({ targetPort: 8000 })
    .withReference(mongoRS);

const goContainer = await builder.addDockerfile('Go-Container', '../../Go/WebApi')
    .withHttpEndpoint({ targetPort: 8080 })
    .withReference(mongoRS);

await builder.build().run();