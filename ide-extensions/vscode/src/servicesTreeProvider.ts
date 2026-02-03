import * as vscode from 'vscode';
import { NeedlrGraph, GraphService } from './types';
import { GraphLoader } from './graphLoader';

/**
 * Tree data provider for the Needlr Services view.
 */
export class ServicesTreeProvider implements vscode.TreeDataProvider<ServiceTreeItem> {
    private readonly _onDidChangeTreeData = new vscode.EventEmitter<ServiceTreeItem | undefined>();
    public readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private graph: NeedlrGraph | undefined;

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

    public getTreeItem(element: ServiceTreeItem): vscode.TreeItem {
        return element;
    }

    public getChildren(element?: ServiceTreeItem): ServiceTreeItem[] {
        if (!this.graph) {
            return [];
        }

        if (!element) {
            // Root level: show lifetime groups
            return [
                new LifetimeGroupItem('Singletons', 'Singleton', this.graph.statistics.singletons),
                new LifetimeGroupItem('Scoped', 'Scoped', this.graph.statistics.scoped),
                new LifetimeGroupItem('Transient', 'Transient', this.graph.statistics.transient),
            ];
        }

        if (element instanceof LifetimeGroupItem) {
            // Show services for this lifetime
            return this.graph.services
                .filter(s => s.lifetime === element.lifetime)
                .map(s => new ServiceItem(s));
        }

        if (element instanceof ServiceItem) {
            // Show service details
            const items: ServiceTreeItem[] = [];

            // Interfaces
            if (element.service.interfaces.length > 0) {
                items.push(new DetailGroupItem('Interfaces', element.service.interfaces.map(i => i.name)));
            }

            // Dependencies
            if (element.service.dependencies.length > 0) {
                items.push(new DetailGroupItem('Dependencies', 
                    element.service.dependencies.map(d => `${d.parameterName}: ${d.typeName}`)));
            }

            // Decorators
            if (element.service.decorators.length > 0) {
                items.push(new DetailGroupItem('Decorators', 
                    element.service.decorators.map(d => `${d.order}: ${d.typeName}`)));
            }

            return items;
        }

        if (element instanceof DetailGroupItem) {
            return element.details.map(d => new DetailItem(d));
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
        count: number
    ) {
        super(`${label} (${count})`, vscode.TreeItemCollapsibleState.Collapsed);
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
        this.tooltip = new vscode.MarkdownString()
            .appendMarkdown(`**${service.fullTypeName}**\n\n`)
            .appendMarkdown(`- Lifetime: ${service.lifetime}\n`)
            .appendMarkdown(`- Dependencies: ${service.dependencies.length}\n`)
            .appendMarkdown(`- Decorators: ${service.decorators.length}\n`);
        
        this.iconPath = new vscode.ThemeIcon('symbol-class');

        if (service.location?.filePath) {
            this.command = {
                command: 'vscode.open',
                title: 'Go to Definition',
                arguments: [
                    vscode.Uri.file(service.location.filePath),
                    { selection: new vscode.Range(
                        service.location.line - 1, 
                        service.location.column - 1,
                        service.location.line - 1,
                        service.location.column - 1
                    )}
                ]
            };
        }
    }
}

class DetailGroupItem extends ServiceTreeItem {
    constructor(
        label: string,
        public readonly details: string[]
    ) {
        super(`${label} (${details.length})`, vscode.TreeItemCollapsibleState.Collapsed);
        this.contextValue = 'detailGroup';
    }
}

class DetailItem extends ServiceTreeItem {
    constructor(detail: string) {
        super(detail, vscode.TreeItemCollapsibleState.None);
        this.contextValue = 'detail';
        this.iconPath = new vscode.ThemeIcon('circle-small');
    }
}
