const { spawnSync } = require('node:child_process');
const { isIPv4 } = require('node:net');

try {
    const required = name => {
        const value = process.env[name];
        if (!value || !value.trim()) throw new Error(`Missing required environment variable: ${name}`);
        return value;
    };
    const profile = required('ATLAS_PROFILE');
    const organizationId = required('ATLAS_ORG_ID');
    const projectName = required('ATLAS_PROJECT_NAME');
    const clusterName = required('ATLAS_CLUSTER_NAME');
    const databaseName = required('Mongo__DatabaseName');
    const appPassword = required('ATLAS_APP_PASSWORD');
    const setupPassword = required('ATLAS_SETUP_PASSWORD');
    const region = process.env.ATLAS_REGION || 'US_EAST_2';
    const allowedIps = [...new Set(required('ATLAS_ALLOWED_IPS').split(',').map(ip => ip.trim()))];
    if (!/^[a-f0-9]{24}$/i.test(organizationId)) throw new Error('Invalid Atlas organization ID.');
    if (!/^[a-z0-9_-]+$/i.test(databaseName) || ['admin', 'local', 'config'].includes(databaseName.toLowerCase()))
        throw new Error('Use a dedicated application database name, not a system database.');
    if (!/^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$/i.test(clusterName))
        throw new Error('Use an Atlas cluster name containing letters, digits and hyphens, up to 64 characters.');
    if (appPassword.length < 20 || setupPassword.length < 20 || appPassword === setupPassword)
        throw new Error('Use different app/setup passwords of at least 20 characters.');
    if (!allowedIps.every(ip => isIPv4(ip) && ip !== '0.0.0.0'))
        throw new Error('ATLAS_ALLOWED_IPS must contain individual IPv4 addresses, never CIDR ranges.');

    const atlas = (args, body) => {
        const result = spawnSync(process.env.ATLAS_EXECUTABLE || 'atlas',
            [...args, '--profile', profile, '--output', 'json'], {
                encoding: 'utf8', timeout: 60000,
                input: body ? JSON.stringify(body) : undefined
            });
        if (result.error || result.status !== 0)
            throw new Error(`Atlas command failed: ${args.slice(0, 3).join(' ')}. No paid fallback was attempted.`);
        try { return JSON.parse(result.stdout); }
        catch { throw new Error(`Atlas returned invalid JSON for ${args.slice(0, 3).join(' ')}.`); }
    };
    const projects = atlas(['projects', 'list', '--orgId', organizationId]).results;
    let project = projects.find(item => item.name === projectName);
    if (!project) project = atlas(['projects', 'create', projectName, '--orgId', organizationId,
        '--tag', 'project=clyvocare-sprint4']);
    if (project.orgId !== organizationId || !project.tags?.some(tag =>
        tag.key === 'project' && tag.value === 'clyvocare-sprint4'))
        throw new Error('Refusing to configure a project not tagged project=clyvocare-sprint4 in this organization.');
    if (!/^[a-f0-9]{24}$/i.test(project.id)) throw new Error('Atlas returned an invalid project ID.');
    const projectArgs = ['--projectId', project.id];

    const regions = atlas(['clusters', 'availableRegions', 'list', '--provider', 'AZURE', '--tier', 'M0', ...projectArgs]);
    const freeRegions = regions.results.filter(provider => provider.provider === 'AZURE')
        .flatMap(provider => provider.instanceSizes.filter(size => size.name === 'M0'))
        .flatMap(size => size.availableRegions);
    if (!freeRegions.some(item => item.name === region))
        throw new Error('Requested Azure region does not offer M0. Select another free region explicitly.');
    const clusters = atlas(['clusters', 'list', ...projectArgs]).results;
    if (clusters.some(item => item.name !== clusterName))
        throw new Error('Use a dedicated project containing only the intended cluster.');
    const cluster = clusters.find(item => item.name === clusterName)
        || atlas(['clusters', 'create', clusterName, '--provider', 'AZURE', '--tier', 'M0', '--region', region, ...projectArgs]);
    const configurations = cluster.replicationSpecs.flatMap(spec => spec.regionConfigs);
    if (configurations.length !== 1 || configurations[0].electableSpecs?.instanceSize !== 'M0'
        || configurations[0].backingProviderName !== 'AZURE' || configurations[0].regionName !== region)
        throw new Error('Refusing to configure a cluster outside the requested Azure M0 region.');

    const accessList = atlas(['accessLists', 'list', ...projectArgs]).results;
    if (accessList.some(entry => !isIPv4(entry.ipAddress) || entry.cidrBlock !== `${entry.ipAddress}/32`))
        throw new Error('Existing network rules must allow individual IPv4 addresses only. Review them manually.');
    const users = atlas(['dbusers', 'list', ...projectArgs]).results;
    const definitions = [
        { username: 'clyvocare_app', password: appPassword, roles: [{ roleName: 'readWrite', databaseName }] },
        { username: 'clyvocare_setup', password: setupPassword, roles: [
            { roleName: 'readWrite', databaseName }, { roleName: 'dbAdmin', databaseName }
        ] }
    ];
    for (const definition of definitions) {
        const existing = users.find(user => user.username === definition.username && user.databaseName === 'admin');
        if (existing) {
            const roleNames = user => user.roles.map(role => `${role.roleName}@${role.databaseName}`).sort().join(',');
            if (roleNames(existing) !== roleNames(definition) || existing.scopes?.length !== 1
                || existing.scopes[0].name !== clusterName || existing.scopes[0].type !== 'CLUSTER')
                throw new Error('Existing database user has different roles or cluster scope. Review it manually.');
        } else {
            // Send passwords on stdin, never as process arguments or output.
            atlas(['api', 'databaseUsers', 'createDatabaseUser', '--groupId', project.id], {
                ...definition, databaseName: 'admin', scopes: [{ name: clusterName, type: 'CLUSTER' }]
            });
        }
    }
    for (const ip of allowedIps) {
        if (!accessList.some(entry => entry.ipAddress === ip))
            atlas(['accessLists', 'create', ip, '--type', 'ipAddress', '--comment', 'ClyvoCare deployment', ...projectArgs]);
    }
    console.log(JSON.stringify({
        projectId: project.id, clusterName, provider: 'AZURE', tier: 'M0', region,
        state: cluster.stateName, srv: cluster.connectionStrings.standardSrv, databaseName,
        appUser: 'clyvocare_app', setupUser: 'clyvocare_setup'
    }, null, 2));
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
