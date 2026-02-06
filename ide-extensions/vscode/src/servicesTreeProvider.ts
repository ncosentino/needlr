import * as vscode from 'vscode';
import { NeedlrGraph, GraphService, GraphDependency } from './types';
import { GraphLoader } from './graphLoader';

/**
 * Tree data provider for the Needlr Services view with search/filter support.
 */
export class ServicesTreeProvider implements vscode.TreeDataProvider<ServiceTreeItem> {
    private readonly _onDidChangeTreeData = new vscode.EventEmitter<ServiceTreeItem | undefined>();
    public readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private graph: NeedlrGraph | undefined;
    private searchFilter: string = '';

    constructor(private readonly graphLoader: GraphLoader) {
        graphLoader.onGraphLoaded(graph => {
            this.graph = graph;
            this._onDidChangeTreeData.fire(undefined);
        });

        graphLoader.onGraphCleared(() => {
            this.graph = undefined;
            this._onDidChangeTreeData.fire(undefined);
        });
    }

    public refresh(): void {
        this._onDidChangeTreeData.fire(undefined);
    }

    public setFilter(filter: string): void {
        this.searchFilter = filter.toLowerCase().trim();
        this._onDidChangeTreeData.fire(undefined);
    }

    public clearFilter(): void {
        this.searchFilter = '';
        this._onDidChangeTreeData.fire(undefined);
    }

    public getTreeItem(element: ServiceTreeItem): vscode.TreeItem {
        return element;
    }

    private matchesFilter(service: GraphService): boolean {
        if (!this.searchFilter) return true;
        
        const filter = this.searchFilter;
        
        // Match type name
        if (service.typeName.toLowerCase().includes(filter)) return true;
        if (service.fullTypeName.toLowerCase().includes(filter)) return true;
        
        // Match interfaces
        if (service.interfaces.some(i => 
            i.name.toLowerCase().includes(filter) || 
            i.fullName.toLowerCase().includes(filter)
        )) return true;
        
        // Match dependencies
        if (service.dependencies.some(d => 
            d.typeName.toLowerCase().includes(filter) ||
            d.parameterName.toLowerCase().includes(filter) ||
            d.resolvedTo?.toLowerCase().includes(filter)
        )) return true;
        
        return false;
    }

    public getChildren(element?: ServiceTreeItem): ServiceTreeItem[] {
        if (!this.graph) {
            return [];
        }

        if (!element) {
            // Root level: show lifetime groups
            const filteredServices = this.graph.services.filter(s => this.matchesFilter(s));
            
            const singletons = filteredServices.filter(s => s.lifetime === 'Singleton');
            const scoped = filteredServices.filter(s => s.lifetime === 'Scoped');
            const transient = filteredServices.filter(s => s.lifetime === 'Transient');

            const items: ServiceTreeItem[] = [];
            
            if (singletons.length > 0 || !this.searchFilter) {
                items.push(new LifetimeGroupItem('Singletons', 'Singleton', singletons.length, singletons));
            }
            if (scoped.length > 0 || !this.searchFilter) {
                items.push(new LifetimeGroupItem('Scoped', 'Scoped', scoped.length, scoped));
            }
            if (transient.length > 0 || !this.searchFilter) {
                items.push(new LifetimeGroupItem('Transient', 'Transient', transient.length, transient));
            }
            
            return items;
        }

        if (element instanceof LifetimeGroupItem) {
            return element.services.map(s => new ServiceItem(s));
        }

        if (element instanceof ServiceItem) {
            // Show service details
            const items: ServiceTreeItem[] = [];

            // Interfaces - no navigation (can't navigate to interface definitions)
            if (element.service.interfaces.length > 0) {
                items.push(new InterfaceGroupItem('Interfaces', element.service.interfaces));
            }

            // Dependencies - pass full dependency info for navigation to implementations
            if (element.service.dependencies.length > 0) {
                items.push(new DependencyGroupItem('Dependencies', element.service.dependencies, this.graph));
            }

            // Decorators
            if (element.service.decorators.length > 0) {
                items.push(new DecoratorGroupItem('Decorators', element.service.decorators, this.graph));
            }

            return items;
        }

        if (element instanceof DependencyGroupItem) {
            return element.dependencies.map(d => new DependencyItem(d, element.graph));
        }

        if (element instanceof InterfaceGroupItem) {
            return element.interfaces.map(i => new InterfaceItem(i));
        }

        if (element instanceof DecoratorGroupItem) {
            return element.decorators.map(d => new DecoratorItem(d, element.graph));
        }

        return [];
    }
}

export abstract class ServiceTreeItem extends vscode.TreeItem {
    constructor(
        label: string,
        collapsibleState: vscode.TreeItemCollapsibleState
    ) {
        super(label, collapsibleState);
    }
}

class LifetimeGroupItem extends ServiceTreeItem {
    constructor(
        label: string,
        public readonly lifetime: 'Singleton' | 'Scoped' | 'Transient',
        count: number,
        public readonly services: GraphService[]
    ) {
        super(`${label} (${count})`, count > 0 ? vscode.TreeItemCollapsibleState.Expanded : vscode.TreeItemCollapsibleState.Collapsed);
        this.contextValue = 'lifetimeGroup';
        this.iconPath = new vscode.ThemeIcon(this.getIcon());
    }

    private getIcon(): string {
        switch (this.lifetime) {
            case 'Singleton': return 'symbol-constant';
            case 'Scoped': return 'symbol-variable';
            case 'Transient': return 'symbol-event';
        }
    }
}

class ServiceItem extends ServiceTreeItem {
    constructor(public readonly service: GraphService) {
        super(service.typeName, vscode.TreeItemCollapsibleState.Collapsed);
        this.contextValue = 'service';
        this.description = service.interfaces.length > 0 
            ? service.interfaces[0].name 
            : undefined;
        
        const hasLocation = service.location?.filePath && service.location.line > 0;
        
        this.tooltip = new vscode.MarkdownString()
            .appendMarkdown(`**${service.fullTypeName}**\n\n`)
            .appendMarkdown(`- Lifetime: ${service.lifetime}\n`)
            .appendMarkdown(`- Dependencies: ${service.dependencies.length}\n`)
            .appendMarkdown(`- Decorators: ${service.decorators.length}\n`)
            .appendMarkdown(hasLocation ? '\n*Click to navigate to source*' : '\n*(Source location not available)*');
        
        this.iconPath = new vscode.ThemeIcon(hasLocation ? 'symbol-class' : 'symbol-interface');

        if (hasLocation) {
            this.command = {
                command: 'vscode.open',
                title: 'Go to Definition',
                arguments: [
                    vscode.Uri.file(service.location!.filePath!),
                    { selection: new vscode.Range(
                        service.location!.line - 1, 
                        0,
                        service.location!.line - 1,
                        0
                    )}
                ]
            };
        }
    }
}

class DependencyGroupItem extends ServiceTreeItem {
    constructor(
        label: string,
        public readonly dependencies: GraphDependency[],
        public readonly graph: NeedlrGraph
    ) {
        super(`${label} (${dependencies.length})`, vscode.TreeItemCollapsibleState.Collapsed);
        this.contextValue = 'dependencyGroup';
    }
}

class DependencyItem extends ServiceTreeItem {
    constructor(dep: GraphDependency, graph: NeedlrGraph) {
        const displayText = `${dep.parameterName}: ${dep.typeName}`;
        super(displayText, vscode.TreeItemCollapsibleState.None);
        this.contextValue = 'dependency';
        this.iconPath = new vscode.ThemeIcon('arrow-right');
        
        // Find the resolved service to enable navigation
        const resolvedService = graph.services.find(s => 
            s.fullTypeName === dep.resolvedTo || 
            s.fullTypeName === dep.fullTypeName ||
            s.interfaces.some(i => i.fullName === dep.fullTypeName)
        );
        
        const hasLocation = resolvedService?.location?.filePath && resolvedService.location.line > 0;
        
        this.description = dep.resolvedTo ? `→ ${getSimpleTypeName(dep.resolvedTo)}` : undefined;
        this.tooltip = hasLocation 
            ? `Click to navigate to ${resolvedService!.typeName}` 
            : `Resolved: ${dep.resolvedTo ?? 'Unknown'}\n(No source location)`;

        if (hasLocation) {
            this.command = {
                command: 'vscode.open',
                title: 'Go to Definition',
                arguments: [
                    vscode.Uri.file(resolvedService!.location!.filePath!),
                    { selection: new vscode.Range(
                        resolvedService!.location!.line - 1, 
                        0,
                        resolvedService!.location!.line - 1,
                        0
                    )}
                ]
            };
        }
    }
}

class InterfaceGroupItem extends ServiceTreeItem {
    constructor(
        label: string,
        public readonly interfaces: Array<{ name: string; fullName: string; location?: { filePath: string | null; line: number; column: number } | null }>
    ) {
        super(`${label} (${interfaces.length})`, vscode.TreeItemCollapsibleState.Collapsed);
        this.contextValue = 'interfaceGroup';
        this.iconPath = new vscode.ThemeIcon('symbol-interface');
    }
}

class InterfaceItem extends ServiceTreeItem {
    constructor(iface: { name: string; fullName: string; location?: { filePath: string | null; line: number; column: number } | null }) {
        super(iface.name, vscode.TreeItemCollapsibleState.None);
        this.contextValue = 'interface';
        this.iconPath = new vscode.ThemeIcon('symbol-interface');
        
        const hasLocation = iface.location?.filePath && iface.location.line > 0;
        this.tooltip = hasLocation ? `Click to navigate to ${iface.fullName}` : iface.fullName;
        
        if (hasLocation) {
            this.command = {
                command: 'vscode.open',
                title: 'Go to Definition',
                arguments: [
                    vscode.Uri.file(iface.location!.filePath!),
                    { selection: new vscode.Range(
                        iface.location!.line - 1,
                        0,
                        iface.location!.line - 1,
                        0
                    )}
                ]
            };
        }
    }
}

class DecoratorGroupItem extends ServiceTreeItem {
    constructor(
        label: string,
        public readonly decorators: Array<{ typeName: string; order: number }>,
        public readonly graph: NeedlrGraph
    ) {
        super(`${label} (${decorators.length})`, vscode.TreeItemCollapsibleState.Collapsed);
        this.contextValue = 'decoratorGroup';
        this.iconPath = new vscode.ThemeIcon('layers');
    }
}

class DecoratorItem extends ServiceTreeItem {
    constructor(decorator: { typeName: string; order: number }, graph: NeedlrGraph) {
        super(`#${decorator.order}: ${decorator.typeName}`, vscode.TreeItemCollapsibleState.None);
        this.contextValue = 'decorator';
        this.iconPath = new vscode.ThemeIcon('layers-active');
        
        // Find the decorator service to enable navigation
        const decoratorService = graph.services.find(s => 
            s.typeName === decorator.typeName ||
            s.fullTypeName.endsWith(decorator.typeName)
        );
        
        const hasLocation = decoratorService?.location?.filePath && decoratorService.location.line > 0;
        this.tooltip = hasLocation ? `Click to navigate to ${decorator.typeName}` : decorator.typeName;

        if (hasLocation) {
            this.command = {
                command: 'vscode.open',
                title: 'Go to Definition',
                arguments: [
                    vscode.Uri.file(decoratorService!.location!.filePath!),
                    { selection: new vscode.Range(
                        decoratorService!.location!.line - 1, 
                        0,
                        decoratorService!.location!.line - 1,
                        0
                    )}
                ]
            };
        }
    }
}

function getSimpleTypeName(fullTypeName: string): string {
    const lastDot = fullTypeName.lastIndexOf('.');
    return lastDot >= 0 ? fullTypeName.substring(lastDot + 1) : fullTypeName;
}
