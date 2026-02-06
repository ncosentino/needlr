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
exports.NeedlrCodeLensProvider = void 0;
const vscode = __importStar(require("vscode"));
/**
 * CodeLens provider that shows dependency information above service classes.
 */
class NeedlrCodeLensProvider {
    graphLoader;
    _onDidChangeCodeLenses = new vscode.EventEmitter();
    onDidChangeCodeLenses = this._onDidChangeCodeLenses.event;
    constructor(graphLoader) {
        this.graphLoader = graphLoader;
        graphLoader.onGraphLoaded(() => this._onDidChangeCodeLenses.fire());
        graphLoader.onGraphCleared(() => this._onDidChangeCodeLenses.fire());
    }
    provideCodeLenses(document) {
        const graph = this.graphLoader.getGraph();
        if (!graph)
            return [];
        const codeLenses = [];
        const text = document.getText();
        const filePath = document.uri.fsPath;
        // Find services that are defined in this file
        for (const service of graph.services) {
            if (!service.location?.filePath)
                continue;
            // Normalize paths for comparison
            const normalizedServicePath = service.location.filePath.replace(/\\/g, '/').toLowerCase();
            const normalizedFilePath = filePath.replace(/\\/g, '/').toLowerCase();
            if (normalizedServicePath !== normalizedFilePath)
                continue;
            if (service.location.line <= 0)
                continue;
            const line = service.location.line - 1;
            if (line >= document.lineCount)
                continue;
            const range = new vscode.Range(line, 0, line, 0);
            // Create the main CodeLens with dependency info
            const label = this.buildLabel(service, graph.services);
            codeLenses.push(new vscode.CodeLens(range, {
                title: label,
                command: 'needlr.showServiceDetails',
                arguments: [service]
            }));
        }
        return codeLenses;
    }
    buildLabel(service, allServices) {
        const parts = [];
        // Lifetime symbol
        const lifetimeSymbol = this.getLifetimeSymbol(service.lifetime);
        parts.push(lifetimeSymbol);
        // Dependency count
        if (service.dependencies.length > 0) {
            parts.push(`${service.dependencies.length} deps`);
        }
        // Used by count
        const usedByCount = this.countUsedBy(service, allServices);
        if (usedByCount > 0) {
            parts.push(`← ${usedByCount}`);
        }
        // Captive dependency warning
        if (this.hasCaptiveDependency(service, allServices)) {
            parts.push('⚠ captive');
        }
        return parts.join(' | ');
    }
    getLifetimeSymbol(lifetime) {
        switch (lifetime) {
            case 'Singleton': return '◆ Singleton';
            case 'Scoped': return '◈ Scoped';
            case 'Transient': return '◇ Transient';
            default: return lifetime;
        }
    }
    countUsedBy(service, allServices) {
        return allServices.filter(s => s.dependencies.some(d => d.fullTypeName === service.fullTypeName ||
            service.interfaces.some(i => i.fullName === d.fullTypeName))).length;
    }
    hasCaptiveDependency(service, allServices) {
        if (service.lifetime !== 'Singleton')
            return false;
        for (const dep of service.dependencies) {
            const resolvedService = allServices.find(s => s.fullTypeName === dep.resolvedTo ||
                s.fullTypeName === dep.fullTypeName);
            if (resolvedService &&
                (resolvedService.lifetime === 'Scoped' || resolvedService.lifetime === 'Transient')) {
                return true;
            }
        }
        return false;
    }
}
exports.NeedlrCodeLensProvider = NeedlrCodeLensProvider;
//# sourceMappingURL=codeLensProvider.js.map