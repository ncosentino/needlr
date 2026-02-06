import * as vscode from 'vscode';
import { GraphLoader } from './graphLoader';
import { GraphService } from './types';

/**
 * CodeLens provider that shows dependency information above service classes.
 */
export class NeedlrCodeLensProvider implements vscode.CodeLensProvider {
    private readonly _onDidChangeCodeLenses = new vscode.EventEmitter<void>();
    public readonly onDidChangeCodeLenses = this._onDidChangeCodeLenses.event;

    constructor(private readonly graphLoader: GraphLoader) {
        graphLoader.onGraphLoaded(() => this._onDidChangeCodeLenses.fire());
        graphLoader.onGraphCleared(() => this._onDidChangeCodeLenses.fire());
    }

    public provideCodeLenses(document: vscode.TextDocument): vscode.CodeLens[] {
        const graph = this.graphLoader.getGraph();
        if (!graph) return [];

        const codeLenses: vscode.CodeLens[] = [];
        const text = document.getText();
        const filePath = document.uri.fsPath;

        // Find services that are defined in this file
        for (const service of graph.services) {
            if (!service.location?.filePath) continue;
            
            // Normalize paths for comparison
            const normalizedServicePath = service.location.filePath.replace(/\\/g, '/').toLowerCase();
            const normalizedFilePath = filePath.replace(/\\/g, '/').toLowerCase();
            
            if (normalizedServicePath !== normalizedFilePath) continue;
            if (service.location.line <= 0) continue;

            const line = service.location.line - 1;
            if (line >= document.lineCount) continue;

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

    private buildLabel(service: GraphService, allServices: GraphService[]): string {
        const parts: string[] = [];
        
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

    private getLifetimeSymbol(lifetime: string): string {
        switch (lifetime) {
            case 'Singleton': return '◆ Singleton';
            case 'Scoped': return '◈ Scoped';
            case 'Transient': return '◇ Transient';
            default: return lifetime;
        }
    }

    private countUsedBy(service: GraphService, allServices: GraphService[]): number {
        return allServices.filter(s => 
            s.dependencies.some(d => 
                d.fullTypeName === service.fullTypeName ||
                service.interfaces.some(i => i.fullName === d.fullTypeName)
            )
        ).length;
    }

    private hasCaptiveDependency(service: GraphService, allServices: GraphService[]): boolean {
        if (service.lifetime !== 'Singleton') return false;
        
        for (const dep of service.dependencies) {
            const resolvedService = allServices.find(s => 
                s.fullTypeName === dep.resolvedTo ||
                s.fullTypeName === dep.fullTypeName
            );
            
            if (resolvedService && 
                (resolvedService.lifetime === 'Scoped' || resolvedService.lifetime === 'Transient')) {
                return true;
            }
        }
        
        return false;
    }
}
