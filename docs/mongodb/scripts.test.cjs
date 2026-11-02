const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const environment = { Mongo__ConnectionString: "mongodb://localhost", Mongo__DatabaseName: "test", CLINICS_JSON_PATH: "clinics.json" };
const clinic = { _id: 1, name: "Test clinic", cnpj: "11.111.111/0001-11", phone: null,
    city: { id: 2, name: "City", state: { id: 3, name: "State", uf: "SP" } } };
const stored = new Map();
let connections = 0;
let failWrite = false;
const schemas = new Map();
const indexes = [];
const database = {
    getCollectionNames: () => [...schemas.keys()],
    createCollection: (name, options) => { schemas.set(name, options); return { ok: 1 }; },
    runCommand: command => { schemas.set(command.collMod, command); return { ok: 1 }; },
    Clinics: {
        createIndex: index => indexes.push(index),
        replaceOne: (filter, document, options) => {
            assert.equal(filter._id, document._id);
            assert.equal(typeof document._id, "bigint");
            assert.equal(typeof document.city.id, "bigint");
            assert.equal(typeof document.city.state.id, "bigint");
            assert.equal(options.upsert, true);
            if (failWrite) throw new Error("Write failed");
            stored.set(filter._id, document);
            return { acknowledged: true };
        },
        countDocuments: () => stored.size
    },
    ClinicAuditLogs: { createIndex: index => indexes.push(index) }
};

function run(script, dataset = [clinic], env = environment) {
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, script), "utf8"), {
        process: { env }, print: () => {}, NumberLong: value => BigInt(value),
        require: name => { assert.equal(name, "fs"); return { readFileSync: () => JSON.stringify(dataset) }; },
        Mongo: function () { connections++; this.getDB = () => database; }
    });
}

assert.throws(() => run("create-collections.js", [], {}), /Set Mongo/);
run("create-collections.js");
run("create-collections.js");
assert.equal(schemas.size, 2);
assert.equal(schemas.get("Clinics").validator.$jsonSchema.properties._id.bsonType, "long");
assert.equal(schemas.get("ClinicAuditLogs").validator.$jsonSchema.properties.occurredAtUtc.bsonType, "date");
assert.equal(indexes.length, 4);
run("import-clinics.js");
run("import-clinics.js", [{ ...clinic, name: "Updated clinic" }]);
assert.equal(stored.size, 1);
assert.equal(stored.get(1n).name, "Updated clinic");
run("import-clinics.js", []);
assert.equal(stored.size, 1);
const before = connections;
for (const badDataset of [{}, [null], [{ ...clinic, _id: 0 }], [{ ...clinic, _id: 9007199254740992 }],
    [clinic, clinic], [{ ...clinic, city: { id: -1 } }]]) {
    assert.throws(() => run("import-clinics.js", badDataset));
}
assert.equal(connections, before);
failWrite = true;
assert.throws(() => run("import-clinics.js"), /Write failed/);
schemas.delete("Clinics");
assert.throws(() => run("import-clinics.js"), /create-collections/);
console.log("MongoDB scripts: assertions passed (isolated shell doubles).");
