import {
    GraphLocation,
    GraphService,
    GraphStatistics,
    NeedlrGraph
} from './types';

/**
 * Merges project-owned Needlr graphs into one workspace graph.
 * Location-rich producer entries replace locationless referenced entries.
 */
export function mergeGraphs(graphs: NeedlrGraph[]): NeedlrGraph | undefined {
    if (graphs.length === 0) {
        return undefined;
    }

    const allServices = new Map<string, GraphService>();
    const interfaceLocations = new Map<string, GraphLocation>();

    for (const graph of graphs) {
        for (const service of graph.services) {
            for (const iface of service.interfaces) {
                if (iface.location?.filePath &&
                    !interfaceLocations.has(iface.fullName)) {
                    interfaceLocations.set(iface.fullName, iface.location);
                }
            }

            const existing = allServices.get(service.fullTypeName);
            if (!existing || hasBetterLocation(service, existing)) {
                allServices.set(service.fullTypeName, service);
            }
        }
    }

    for (const service of allServices.values()) {
        for (const iface of service.interfaces) {
            if (!iface.location) {
                iface.location = interfaceLocations.get(iface.fullName);
            }
        }
    }

    if (allServices.size === 0) {
        return undefined;
    }

    const primary = graphs[0];
    const services = Array.from(allServices.values());
    return {
        schemaVersion: '1.0',
        generatedAt: new Date().toISOString(),
        assemblyName: primary.assemblyName ?? 'Merged',
        projectPath: primary.projectPath,
        services,
        diagnostics: [],
        statistics: calculateStatistics(services)
    };
}

function hasBetterLocation(
    candidate: GraphService,
    existing: GraphService
): boolean {
    const candidateHasLocation =
        !!candidate.location?.filePath && candidate.location.line > 0;
    const existingHasLocation =
        !!existing.location?.filePath && existing.location.line > 0;
    return candidateHasLocation && !existingHasLocation;
}

function calculateStatistics(services: GraphService[]): GraphStatistics {
    return {
        totalServices: services.length,
        singletons: services.filter(
            service => service.lifetime === 'Singleton').length,
        scoped: services.filter(
            service => service.lifetime === 'Scoped').length,
        transient: services.filter(
            service => service.lifetime === 'Transient').length,
        decorators: services.reduce(
            (sum, service) => sum + service.decorators.length,
            0),
        interceptors: services.reduce(
            (sum, service) => sum + service.interceptors.length,
            0),
        factories: services.filter(
            service => service.metadata.hasFactory).length,
        options: services.filter(
            service => service.metadata.hasOptions).length,
        hostedServices: services.filter(
            service => service.metadata.isHostedService).length,
        plugins: services.filter(
            service => service.metadata.isPlugin).length
    };
}
