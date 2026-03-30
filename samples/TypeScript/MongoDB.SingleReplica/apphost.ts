// Aspire TypeScript AppHost
// For more information, see: https://aspire.dev

import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const mongoServer = await builder.addMongoDB('TestMongo', { port: 27017 });

await mongoServer.addDatabase('TestMongoDB');

const mongoRS = await builder.addMongoDBReplicaSet('TestMongoRS')
    .withMember(mongoServer)
    .withDbGate();

await builder.build().run();