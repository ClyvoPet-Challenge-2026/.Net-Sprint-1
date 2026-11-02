const fs = require("fs");

if (!process.env.Mongo__ConnectionString || !process.env.Mongo__DatabaseName || !process.env.CLINICS_JSON_PATH) {
    throw new Error("Set Mongo__ConnectionString, Mongo__DatabaseName and CLINICS_JSON_PATH.");
}

const documents = JSON.parse(fs.readFileSync(process.env.CLINICS_JSON_PATH, "utf8"));
if (!Array.isArray(documents)) throw new Error("The clinic dataset must be a JSON array.");

// shortcut: Native JSON IDs must fit safe integers; use Extended JSON if IDs exceed this range.
function referenceId(value) {
    if (!Number.isSafeInteger(value) || value <= 0) throw new Error("Clinic, city and state IDs must be positive safe integers.");
    return NumberLong(String(value));
}

const seen = new Set();
for (const document of documents) {
    if (seen.has(document?._id)) throw new Error("Duplicate clinic ID in dataset.");
    seen.add(document?._id);
    document._id = referenceId(document?._id);
    document.city.id = referenceId(document.city?.id);
    document.city.state.id = referenceId(document.city.state?.id);
}

const database = new Mongo(process.env.Mongo__ConnectionString).getDB(process.env.Mongo__DatabaseName);
if (!database.getCollectionNames().includes("Clinics")) {
    throw new Error("Run create-collections.js before importing clinics.");
}

for (const document of documents) {
    const result = database.Clinics.replaceOne({ _id: document._id }, document, { upsert: true });
    if (!result.acknowledged) throw new Error("Clinic import was not acknowledged.");
}
print(`Imported ${documents.length} clinics; collection now contains ${database.Clinics.countDocuments({})} documents.`);
