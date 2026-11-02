const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const vm = require('node:vm');

const source = readFileSync(join(__dirname, 'provision-atlas.cjs'), 'utf8');
const organizationId = '0123456789abcdef01234567';
const project = {
    id: '1123456789abcdef01234567', orgId: organizationId, name: 'clyvocare-test',
    tags: [{ key: 'project', value: 'clyvocare-sprint4' }]
};
const cluster = {
    name: 'clyvocare-test', stateName: 'IDLE', connectionStrings: { standardSrv: 'mongodb+srv://example.invalid' },
    replicationSpecs: [{ regionConfigs: [{
        electableSpecs: { instanceSize: 'M0' }, backingProviderName: 'AZURE', regionName: 'US_EAST_2'
    }] }]
};
const environment = {
    ATLAS_PROFILE: 'test', ATLAS_ORG_ID: organizationId, ATLAS_PROJECT_NAME: project.name,
    ATLAS_CLUSTER_NAME: cluster.name, Mongo__DatabaseName: 'clyvocare',
    ATLAS_APP_PASSWORD: 'app-secret-for-unit-test-only',
    ATLAS_SETUP_PASSWORD: 'setup-secret-for-unit-test-only', ATLAS_ALLOWED_IPS: '203.0.113.1'
};
const run = (options = {}) => {
    const calls = [];
    const stdout = [], stderr = [];
    const process = { env: { ...environment, ...options.env }, exitCode: 0 };
    const response = args => {
        if (args[0] === 'projects') return args[1] === 'list' ? { results: options.projects || [] } : project;
        if (args[1] === 'availableRegions') return { results: [{ provider: 'AZURE', instanceSizes: [
            { name: 'M0', availableRegions: [{ name: options.region || 'US_EAST_2' }] }
        ] }] };
        if (args[0] === 'clusters') return args[1] === 'list' ? { results: options.clusters || [] } : cluster;
        if (args[0] === 'accessLists') return { results: options.accessList || [] };
        if (args[0] === 'dbusers') return { results: options.users || [] };
        return {};
    };
    const spawnSync = (executable, args, config) => {
        calls.push({ executable, args, input: config.input });
        if (options.fail) return { status: 17, stderr: environment.ATLAS_APP_PASSWORD };
        if (options.malformed) return { status: 0, stdout: environment.ATLAS_APP_PASSWORD };
        return { status: 0, stdout: JSON.stringify(response(args)) };
    };
    vm.runInNewContext(source, {
        require: name => name === 'node:child_process' ? { spawnSync } : require(name), process,
        console: { log: value => stdout.push(value), error: value => stderr.push(value) }
    }, { timeout: 1000 });
    const result = { calls, code: process.exitCode, stdout: stdout.join('\n'), stderr: stderr.join('\n') };
    for (const password of [environment.ATLAS_APP_PASSWORD, environment.ATLAS_SETUP_PASSWORD]) {
        assert.ok(!result.stdout.includes(password) && !result.stderr.includes(password));
        assert.ok(calls.every(call => !call.args.includes(password)));
    }
    return result;
};

const created = run();
assert.equal(created.code, 0, created.stderr);
const creation = created.calls.find(call => call.args[0] === 'clusters' && call.args[1] === 'create');
assert.equal(creation.args[creation.args.indexOf('--tier') + 1], 'M0');
assert.equal(creation.args[creation.args.indexOf('--provider') + 1], 'AZURE');
const userCalls = created.calls.filter(call => call.args[1] === 'databaseUsers');
assert.equal(userCalls.length, 2);
const users = userCalls.map(call => JSON.parse(call.input));
assert.equal(users[0].roles.length, 1);
assert.equal(users[0].roles[0].roleName, 'readWrite');
assert.equal(users[1].roles.length, 2);
const reused = run({ projects: [project], clusters: [cluster], users: users.map(user => ({ ...user, password: undefined })),
    accessList: [{ ipAddress: '203.0.113.1', cidrBlock: '203.0.113.1/32' }] });
assert.equal(reused.code, 0, reused.stderr);
assert.ok(reused.calls.every(call => !call.args.includes('create') && !call.args.includes('createDatabaseUser')));

for (const name of Object.keys(environment)) {
    const result = run({ env: { [name]: '' } });
    assert.equal(result.code, 1, `Missing ${name} was accepted.`);
    assert.equal(result.calls.length, 0);
}
for (const env of [
    { ATLAS_ORG_ID: 'invalid' }, { ATLAS_CLUSTER_NAME: 'invalid/name' }, { Mongo__DatabaseName: 'admin' },
    { ATLAS_ALLOWED_IPS: '0.0.0.0/0' }, { ATLAS_APP_PASSWORD: 'weak' },
    { ATLAS_SETUP_PASSWORD: environment.ATLAS_APP_PASSWORD }
]) assert.equal(run({ env }).code, 1);
const configuration = cluster.replicationSpecs[0].regionConfigs[0];
for (const changed of [
    { ...configuration, electableSpecs: { instanceSize: 'M10' } },
    { ...configuration, backingProviderName: 'AWS' },
    { ...configuration, regionName: 'US_WEST' }
]) {
    const result = run({ projects: [project], clusters: [{ ...cluster, replicationSpecs: [{ regionConfigs: [changed] }] }] });
    assert.equal(result.code, 1);
    assert.ok(!result.calls.some(call => call.args[1] === 'databaseUsers'));
}
assert.equal(run({ projects: [{ ...project, tags: [] }] }).code, 1);
assert.equal(run({ projects: [project], clusters: [{ ...cluster, name: 'unrelated' }] }).code, 1);
assert.equal(run({ region: 'US_WEST' }).code, 1);
assert.equal(run({ projects: [project], clusters: [cluster], accessList: [{ cidrBlock: '0.0.0.0/0' }] }).code, 1);
assert.equal(run({ projects: [project], clusters: [cluster], users: [{ ...users[0], roles: [] }] }).code, 1);
const failed = run({ fail: true });
assert.equal(failed.code, 1);
assert.equal(failed.calls.length, 1);
assert.equal(run({ malformed: true }).code, 1);
console.log('Atlas M0 provisioning: assertions passed.');
