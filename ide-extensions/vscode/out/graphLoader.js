"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.GraphLoader = void 0;
const vscode = __importStar(require("vscode"));
const fs = __importStar(require("fs"));
/**
 * Loads and merges Needlr graph files from all projects in the workspace.
 */
class GraphLoader {
    context;
    _onGraphLoaded = new vscode.EventEmitter();
    onGraphLoaded = this._onGraphLoaded.event;
    _onGraphCleared = new vscode.EventEmitter();
    onGraphCleared = this._onGraphCleared.event;
    fileWatchers = [];
    currentGraph;
    constructor(context) {
        this.context = context;
    }
    async initialize() {
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
    async findAndLoadGraph() {
        const allServices = new Map();
        const interfaceLocations = new Map();
        let primaryAssemblyName = null;
        let primaryProjectPath = null;
        // Find all NeedlrGraph.g.cs files (each project with [GenerateTypeRegistry] has one)
        const sourceFiles = await vscode.workspace.findFiles('**/NeedlrGraph.g.cs', '**/node_modules/**');
        console.log(`Needlr: Found ${sourceFiles.length} NeedlrGraph.g.cs files`);
        // First pass: collect all interface locations from all graphs
        for (const uri of sourceFiles) {
            const graph = await this.loadGraphFromSourceFileInternal(uri);
            if (!graph)
                continue;
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
                }
                else if (this.hasBetterLocation(service, existing)) {
                    allServices.set(service.fullTypeName, service);
                }
            }
        }
        // Also check for needlr-graph.json files
        const jsonFiles = await vscode.workspace.findFiles('**/obj/**/needlr-graph.json', '**/node_modules/**');
        for (const uri of jsonFiles) {
            const graph = await this.loadGraphFileInternal(uri);
            if (!graph)
                continue;
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
                }
                else if (this.hasBetterLocation(service, existing)) {
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
        const mergedGraph = {
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
    hasBetterLocation(newService, existing) {
        const newHasLocation = newService.location?.filePath && newService.location.line > 0;
        const existingHasLocation = existing.location?.filePath && existing.location.line > 0;
        return !!newHasLocation && !existingHasLocation;
    }
    calculateStatistics(services) {
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
    async loadGraphFileInternal(uri) {
        try {
            const content = await fs.promises.readFile(uri.fsPath, 'utf-8');
            return JSON.parse(content);
        }
        catch (error) {
            console.error('Failed to load Needlr graph:', error);
            return undefined;
        }
    }
    async loadGraphFromSourceFileInternal(uri) {
        try {
            const content = await fs.promises.readFile(uri.fsPath, 'utf-8');
            // Try raw string literal format first (C# 11+)
            let match = content.match(/GraphJson(?:Content)?\s*=\s*"""([\s\S]*?)"""/);
            if (match) {
                return JSON.parse(match[1]);
            }
            // Try verbatim string format @"..."
            match = content.match(/GraphJson(?:Content)?\s*=\s*@"((?:[^"]|"")*)"/s);
            if (match) {
                const jsonString = match[1].replace(/""/g, '"');
                return JSON.parse(jsonString);
            }
            console.log(`Could not find GraphJson pattern in ${uri.fsPath}`);
            return undefined;
        }
        catch (error) {
            console.error('Failed to extract graph from source file:', error);
            return undefined;
        }
    }
    clearGraph() {
        this.currentGraph = undefined;
        this._onGraphCleared.fire();
        vscode.commands.executeCommand('setContext', 'needlr:hasGraph', false);
    }
    getGraph() {
        return this.currentGraph;
    }
    dispose() {
        for (const watcher of this.fileWatchers) {
            watcher.dispose();
        }
        this._onGraphLoaded.dispose();
        this._onGraphCleared.dispose();
    }
}
exports.GraphLoader = GraphLoader;
//# sourceMappingURL=graphLoader.js.map