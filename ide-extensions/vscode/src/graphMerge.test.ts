import assert from 'node:assert/strict';
import test from 'node:test';

import { mergeGraphs } from './graphMerge';
import { GraphService, NeedlrGraph } from './types';

test('producer locations replace locationless referenced entries', () => {
    const referencedService = createService(null, null);
    const producerService = createService(
        {
            filePath: 'Feature/FeatureService.cs',
            line: 12,
            column: 0
        },
        {
            filePath: 'Feature/IFeatureService.cs',
            line: 4,
            column: 0
        });
    const hostService = createHostService();

    const merged = mergeGraphs([
        createGraph('Host', [hostService, referencedService]),
        createGraph('Feature', [producerService])
    ]);

    assert.ok(merged);
    assert.equal(merged.schemaVersion, '1.0');
    assert.equal(merged.services.length, 2);

    const feature = merged.services.find(
        service =>
            service.fullTypeName === 'global::Feature.FeatureService');
    assert.equal(
        feature?.location?.filePath,
        'Feature/FeatureService.cs');
    assert.equal(
        feature?.interfaces[0].location?.filePath,
        'Feature/IFeatureService.cs');
});

test('interface locations are backfilled across distinct services', () => {
    const first = createService(null, null);
    const second = createService(
        {
            filePath: 'Feature/SecondService.cs',
            line: 20,
            column: 0
        },
        {
            filePath: 'Feature/IFeatureService.cs',
            line: 4,
            column: 0
        });
    second.id = 'global::Feature.SecondService';
    second.typeName = 'SecondService';
    second.fullTypeName = 'global::Feature.SecondService';

    const merged = mergeGraphs([
        createGraph('First', [first]),
        createGraph('Second', [second])
    ]);

    assert.equal(
        merged?.services.find(
            service =>
                service.fullTypeName ===
                'global::Feature.FeatureService')
            ?.interfaces[0].location?.filePath,
        'Feature/IFeatureService.cs');
});

function createGraph(
    assemblyName: string,
    services: GraphService[]
): NeedlrGraph {
    return {
        schemaVersion: '1.0',
        generatedAt: '2026-07-24T00:00:00.000Z',
        projectPath: null,
        assemblyName,
        services,
        diagnostics: [],
        statistics: {
            totalServices: services.length,
            singletons: services.length,
            scoped: 0,
            transient: 0,
            decorators: 0,
            interceptors: 0,
            factories: 0,
            options: 0,
            hostedServices: 0,
            plugins: 0
        }
    };
}

function createService(
    location: GraphService['location'],
    interfaceLocation:
        GraphService['interfaces'][number]['location']
): GraphService {
    return {
        id: 'global::Feature.FeatureService',
        typeName: 'FeatureService',
        fullTypeName: 'global::Feature.FeatureService',
        interfaces: [
            {
                name: 'IFeatureService',
                fullName: 'global::Feature.IFeatureService',
                location: interfaceLocation
            }
        ],
        lifetime: 'Singleton',
        location,
        dependencies: [],
        decorators: [],
        interceptors: [],
        attributes: [],
        serviceKeys: [],
        metadata: {
            hasFactory: false,
            hasOptions: false,
            isHostedService: false,
            isDisposable: false,
            isPlugin: false
        }
    };
}

function createHostService(): GraphService {
    return {
        ...createService(
            {
                filePath: 'Host/HostService.cs',
                line: 8,
                column: 0
            },
            null),
        id: 'global::Host.HostService',
        typeName: 'HostService',
        fullTypeName: 'global::Host.HostService',
        interfaces: []
    };
}
