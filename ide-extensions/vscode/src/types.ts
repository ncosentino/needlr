/**
 * Needlr Graph Types
 * TypeScript definitions matching the JSON schema from needlr-graph-v1.schema.json
 */

export interface NeedlrGraph {
    schemaVersion: string;
    generatedAt: string;
    projectPath: string | null;
    assemblyName: string | null;
    services: GraphService[];
    diagnostics: GraphDiagnostic[];
    statistics: GraphStatistics;
}

export interface GraphService {
    id: string;
    typeName: string;
    fullTypeName: string;
    interfaces: GraphInterface[];
    lifetime: 'Singleton' | 'Scoped' | 'Transient';
    location: GraphLocation | null;
    dependencies: GraphDependency[];
    decorators: GraphDecorator[];
    interceptors: string[];
    attributes: string[];
    serviceKeys: string[];
    metadata: GraphServiceMetadata;
}

export interface GraphInterface {
    name: string;
    fullName: string;
}

export interface GraphLocation {
    filePath: string | null;
    line: number;
    column: number;
}

export interface GraphDependency {
    parameterName: string;
    typeName: string;
    fullTypeName: string;
    resolvedTo: string | null;
    resolvedLifetime: string | null;
    isKeyed: boolean;
    serviceKey: string | null;
}

export interface GraphDecorator {
    typeName: string;
    order: number;
}

export interface GraphServiceMetadata {
    hasFactory: boolean;
    hasOptions: boolean;
    isHostedService: boolean;
    isDisposable: boolean;
    isPlugin: boolean;
}

export interface GraphDiagnostic {
    id: string;
    severity: 'Error' | 'Warning' | 'Info';
    message: string;
    location: GraphLocation | null;
    relatedServices: string[];
}

export interface GraphStatistics {
    totalServices: number;
    singletons: number;
    scoped: number;
    transient: number;
    decorators: number;
    interceptors: number;
    factories: number;
    options: number;
    hostedServices: number;
    plugins: number;
}
