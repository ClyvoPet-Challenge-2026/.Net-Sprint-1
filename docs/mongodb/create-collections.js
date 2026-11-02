if (!process.env.Mongo__ConnectionString || !process.env.Mongo__DatabaseName) {
    throw new Error("Set Mongo__ConnectionString and Mongo__DatabaseName.");
}

const database = new Mongo(process.env.Mongo__ConnectionString).getDB(process.env.Mongo__DatabaseName);
const referenceId = { bsonType: "long", minimum: 1 };
const state = {
    bsonType: "object", required: ["id", "name", "uf"],
    properties: { id: referenceId, name: { bsonType: "string" }, uf: { bsonType: "string" } }
};
const city = {
    bsonType: "object", required: ["id", "name", "state"],
    properties: { id: referenceId, name: { bsonType: "string" }, state }
};
const schemas = {
    Clinics: {
        bsonType: "object", required: ["_id", "name", "cnpj", "city"],
        properties: {
            _id: referenceId, name: { bsonType: "string" }, cnpj: { bsonType: "string" },
            phone: { bsonType: ["string", "null"] }, city
        }
    },
    ClinicAuditLogs: {
        bsonType: "object", required: ["clinicId", "operation", "actorId", "occurredAtUtc"],
        properties: {
            clinicId: referenceId, actorId: referenceId,
            operation: { enum: ["CREATE", "UPDATE", "DELETE"] },
            occurredAtUtc: { bsonType: "date" }, traceId: { bsonType: ["string", "null"] }
        }
    }
};

for (const [name, schema] of Object.entries(schemas)) {
    const options = { validator: { $jsonSchema: schema }, validationLevel: "strict", validationAction: "error" };
    const result = database.getCollectionNames().includes(name)
        ? database.runCommand({ collMod: name, ...options })
        : database.createCollection(name, options);
    if (result.ok !== 1) throw new Error(`Cannot configure collection ${name}.`);
}
database.Clinics.createIndex({ "city.id": 1, name: 1 });
database.ClinicAuditLogs.createIndex({ clinicId: 1, occurredAtUtc: -1 });
print("Clinics and ClinicAuditLogs are ready.");
