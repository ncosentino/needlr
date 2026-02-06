import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { NeedlrGraph, GraphService, GraphLocation } from './types';

/**
 * Loads and merges Needlr graph files from all projects in the workspace.
 */
export class GraphLoader implements vscode.Disposable {
    private readonly _onGraphLoaded = new vscode.EventEmitter<NeedlrGraph>();
    public readonly onGraphLoaded = this._onGraphLoaded.event;

    private readonly _onGraphCleared = new vscode.EventEmitter<void>();
    public readonly onGraphCleared = this._onGraphCleared.event;

    private fileWatchers: vscode.FileSystemWatcher[] = [];
    private currentGraph: NeedlrGraph | undefined;

    constructor(private readonly context: vscode.ExtensionContext) {}

    public async initialize(): Promise<void> {
        // Set up file watchers for graph files
        const jsonWatcher = vscode.workspace.createFileSystemWatcher('**/obj/**/needlr-graph.json');
        const sourceWatcher = vscode.workspace.createFileSystemWatcher('**/NeedlrGraph.g.cs');
        
        jsonWatcher.onDidCreate(() => this.findAndLoadGraph());
        jsonWatcher.onDidChange(() => this.findAndLoadGraph());
        jsonWatcher.onDidDelete(() => this.findAndLoadGraph());
        
        sourceWatcher.onDidCreate(() => this.findAndLoadGraph());
        sourceWatcher.onDidChange(() => this.findAndLoadGraph());
        sourceWatcher.onDidDelete(() => this.findAndLoadGraph());

        this.fileWatchers.push(jsonWatcher, sourceWatcher);

        // Initial load
        await this.findAndLoadGraph();
    }

    public async findAndLoadGraph(): Promise<NeedlrGraph | undefined> {
        const allServices = new Map<string, GraphService>();
        const interfaceLocations = new Map<string, GraphLocation>();
        let primaryAssemblyName: string | null = null;
        let primaryProjectPath: string | null = null;

        // Find all NeedlrGraph.g.cs files (each project with [GenerateTypeRegistry] has one)
        const sourceFiles = await vscode.workspace.findFiles('**/NeedlrGraph.g.cs', '**/node_modules/**');
        console.log(`Needlr: Found ${sourceFiles.length} NeedlrGraph.g.cs files`);

        // First pass: collect all interface locations from all graphs
        for (const uri of sourceFiles) {
            const graph = await this.loadGraphFromSourceFileInternal(uri);
            if (!graph) continue;

            console.log(`Needlr: Loaded graph from ${uri.fsPath} with ${graph.services.length} services`);

            if (!primaryAssemblyName) {
                primaryAssemblyName = graph.assemblyName;
                primaryProjectPath = graph.projectPath;
            }

            // Collect interface locations
            for (const service of graph.services) {
                for (const iface of service.interfaces) {
                    if (iface.location?.filePath && !interfaceLocations.has(iface.fullName)) {
                        interfaceLocations.set(iface.fullName, iface.location);
                    }
                }
            }

            // Merge services - prefer entries with source locations
            for (const service of graph.services) {
                const existing = allServices.get(service.fullTypeName);
                if (!existing) {
                    allServices.set(service.fullTypeName, service);
                } else if (this.hasBetterLocation(service, existing)) {
                    allServices.set(service.fullTypeName, service);
                }
            }
        }

        // Also check for needlr-graph.json files
        const jsonFiles = await vscode.workspace.findFiles('**/obj/**/needlr-graph.json', '**/node_modules/**');
        
        for (const uri of jsonFiles) {
            const graph = await this.loadGraphFileInternal(uri);
            if (!graph) continue;

            console.log(`Needlr: Loaded graph from ${uri.fsPath} with ${graph.services.length} services`);

            if (!primaryAssemblyName) {
                primaryAssemblyName = graph.assemblyName;
                primaryProjectPath = graph.projectPath;
            }

            // Collect interface locations
            for (const service of graph.services) {
                for (const iface of service.interfaces) {
                    if (iface.location?.filePath && !interfaceLocations.has(iface.fullName)) {
                        interfaceLocations.set(iface.fullName, iface.location);
                    }
                }
            }

            for (const service of graph.services) {
                const existing = allServices.get(service.fullTypeName);
                if (!existing) {
                    allServices.set(service.fullTypeName, service);
                } else if (this.hasBetterLocation(service, existing)) {
                    allServices.set(service.fullTypeName, service);
                }
            }
        }

        // Apply collected interface locations to all services
        for (const service of allServices.values()) {
            for (const iface of service.interfaces) {
                if (!iface.location && interfaceLocations.has(iface.fullName)) {
                    iface.location = interfaceLocations.get(iface.fullName);
                }
            }
        }

        if (allServices.size === 0) {
            console.log('Needlr: No services found in any graph');
            this.clearGraph();
            return undefined;
        }

        // Create merged graph
        const services = Array.from(allServices.values());
        const mergedGraph: NeedlrGraph = {
            schemaVersion: '1.0',
            generatedAt: new Date().toISOString(),
            assemblyName: primaryAssemblyName ?? 'Merged',
            projectPath: primaryProjectPath,
            services,
            diagnostics: [],
            statistics: this.calculateStatistics(services)
        };

        this.currentGraph = mergedGraph;
        this._onGraphLoaded.fire(mergedGraph);
        await vscode.commands.executeCommand('setContext', 'needlr:hasGraph', true);

        console.log(`Needlr: Merged graph has ${mergedGraph.services.length} services`);
        return mergedGraph;
    }

    private hasBetterLocation(newService: GraphService, existing: GraphService): boolean {
        const newHasLocation = newService.location?.filePath && newService.location.line > 0;
        const existingHasLocation = existing.location?.filePath && existing.location.line > 0;
        return !!newHasLocation && !existingHasLocation;
    }

    private calculateStatistics(services: GraphService[]) {
        return {
            totalServices: services.length,
            singletons: services.filter(s => s.lifetime === 'Singleton').length,
            scoped: services.filter(s => s.lifetime === 'Scoped').length,
            transient: services.filter(s => s.lifetime === 'Transient').length,
            decorators: services.reduce((sum, s) => sum + s.decorators.length, 0),
            interceptors: services.reduce((sum, s) => sum + s.interceptors.length, 0),
            factories: services.filter(s => s.metadata.hasFactory).length,
            options: services.filter(s => s.metadata.hasOptions).length,
            hostedServices: services.filter(s => s.metadata.isHostedService).length,
            plugins: services.filter(s => s.metadata.isPlugin).length
        };
    }

    private async loadGraphFileInternal(uri: vscode.Uri): Promise<NeedlrGraph | undefined> {
        try {
            const content = await fs.promises.readFile(uri.fsPath, 'utf-8');
            return JSON.parse(content) as NeedlrGraph;
        } catch (error) {
            console.error('Failed to load Needlr graph:', error);
            return undefined;
        }
    }

    private async loadGraphFromSourceFileInternal(uri: vscode.Uri): Promise<NeedlrGraph | undefined> {
        try {
            const content = await fs.promises.readFile(uri.fsPath, 'utf-8');
            
            // Try raw string literal format first (C# 11+)
            let match = content.match(/GraphJson(?:Content)?\s*=\s*"""([\s\S]*?)"""/);
            if (match) {
                return JSON.parse(match[1]) as NeedlrGraph;
            }

            // Try verbatim string format @"..."
            match = content.match(/GraphJson(?:Content)?\s*=\s*@"((?:[^"]|"")*)"/s);
            if (match) {
                const jsonString = match[1].replace(/""/g, '"');
                return JSON.parse(jsonString) as NeedlrGraph;
            }

            console.log(`Could not find GraphJson pattern in ${uri.fsPath}`);
            return undefined;
        } catch (error) {
            console.error('Failed to extract graph from source file:', error);
            return undefined;
        }
    }

    private clearGraph(): void {
        this.currentGraph = undefined;
        this._onGraphCleared.fire();
        vscode.commands.executeCommand('setContext', 'needlr:hasGraph', false);
    }

    public getGraph(): NeedlrGraph | undefined {
        return this.currentGraph;
    }

    public dispose(): void {
        for (const watcher of this.fileWatchers) {
            watcher.dispose();
        }
        this._onGraphLoaded.dispose();
        this._onGraphCleared.dispose();
    }
}
