import * as vscode from 'vscode';
import { GraphLoader } from './graphLoader';
import { ServicesTreeProvider } from './servicesTreeProvider';

let graphLoader: GraphLoader;
let servicesTreeProvider: ServicesTreeProvider;

export async function activate(context: vscode.ExtensionContext) {
    console.log('Needlr Tools extension is now active');

    // Initialize graph loader
    graphLoader = new GraphLoader(context);
    context.subscriptions.push(graphLoader);

    // Initialize services tree view
    servicesTreeProvider = new ServicesTreeProvider(graphLoader);
    const treeView = vscode.window.createTreeView('needlrServices', {
        treeDataProvider: servicesTreeProvider,
        showCollapseAll: true
    });
    context.subscriptions.push(treeView);

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('needlr.showDependencyGraph', showDependencyGraph),
        vscode.commands.registerCommand('needlr.refreshGraph', refreshGraph),
        vscode.commands.registerCommand('needlr.goToService', goToService)
    );

    // Initialize the graph loader
    await graphLoader.initialize();

    // Show welcome message if no graph found
    const graph = graphLoader.getGraph();
    if (!graph) {
        vscode.window.showInformationMessage(
            'Needlr Tools: No dependency graph found. Build your project with NeedlrExportGraph=true to generate one.',
            'Learn More'
        ).then(selection => {
            if (selection === 'Learn More') {
                vscode.env.openExternal(vscode.Uri.parse('https://github.com/ncosentino/needlr'));
            }
        });
    }
}

async function showDependencyGraph() {
    const graph = graphLoader.getGraph();
    
    if (!graph) {
        vscode.window.showWarningMessage('No Needlr graph available. Build your project first.');
        return;
    }

    // Create and show a webview panel with the dependency graph
    const panel = vscode.window.createWebviewPanel(
        'needlrGraph',
        'Needlr Dependency Graph',
        vscode.ViewColumn.One,
        {
            enableScripts: true
        }
    );

    panel.webview.html = generateGraphHtml(graph);
}

async function refreshGraph() {
    const graph = await graphLoader.findAndLoadGraph();
    
    if (graph) {
        servicesTreeProvider.refresh();
        vscode.window.showInformationMessage(
            `Needlr: Loaded ${graph.statistics.totalServices} services`
        );
    } else {
        vscode.window.showWarningMessage('No Needlr graph found in workspace.');
    }
}

async function goToService() {
    const graph = graphLoader.getGraph();
    
    if (!graph) {
        vscode.window.showWarningMessage('No Needlr graph available.');
        return;
    }

    const items = graph.services.map(s => ({
        label: s.typeName,
        description: s.lifetime,
        detail: s.fullTypeName,
        service: s
    }));

    const selected = await vscode.window.showQuickPick(items, {
        placeHolder: 'Select a service to navigate to',
        matchOnDescription: true,
        matchOnDetail: true
    });

    if (selected?.service.location?.filePath) {
        const uri = vscode.Uri.file(selected.service.location.filePath);
        const position = new vscode.Position(
            selected.service.location.line - 1,
            selected.service.location.column - 1
        );
        
        const document = await vscode.workspace.openTextDocument(uri);
        const editor = await vscode.window.showTextDocument(document);
        editor.selection = new vscode.Selection(position, position);
        editor.revealRange(new vscode.Range(position, position));
    }
}

function generateGraphHtml(graph: import('./types').NeedlrGraph): string {
    const services = graph.services.map(s => `
        <div class="service ${s.lifetime.toLowerCase()}">
            <div class="service-header">
                <span class="lifetime-badge">${s.lifetime}</span>
                <strong>${escapeHtml(s.typeName)}</strong>
            </div>
            <div class="service-details">
                <small>${escapeHtml(s.fullTypeName)}</small>
                ${s.dependencies.length > 0 ? `
                    <div class="dependencies">
                        <strong>Dependencies:</strong>
                        <ul>
                            ${s.dependencies.map(d => `<li>${escapeHtml(d.typeName)}</li>`).join('')}
                        </ul>
                    </div>
                ` : ''}
            </div>
        </div>
    `).join('');

    return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Needlr Dependency Graph</title>
    <style>
        body {
            font-family: var(--vscode-font-family);
            padding: 20px;
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
        }
        .stats {
            display: flex;
            gap: 20px;
            margin-bottom: 20px;
            padding: 10px;
            background: var(--vscode-editor-inactiveSelectionBackground);
            border-radius: 4px;
        }
        .stat {
            text-align: center;
        }
        .stat-value {
            font-size: 24px;
            font-weight: bold;
        }
        .service {
            border: 1px solid var(--vscode-panel-border);
            border-radius: 4px;
            padding: 10px;
            margin-bottom: 10px;
        }
        .service.singleton { border-left: 3px solid #4CAF50; }
        .service.scoped { border-left: 3px solid #2196F3; }
        .service.transient { border-left: 3px solid #FF9800; }
        .service-header {
            display: flex;
            align-items: center;
            gap: 10px;
        }
        .lifetime-badge {
            font-size: 10px;
            padding: 2px 6px;
            border-radius: 10px;
            background: var(--vscode-badge-background);
            color: var(--vscode-badge-foreground);
        }
        .dependencies {
            margin-top: 10px;
            padding-left: 20px;
        }
        .dependencies ul {
            margin: 5px 0;
            padding-left: 20px;
        }
    </style>
</head>
<body>
    <h1>Needlr Dependency Graph</h1>
    <div class="stats">
        <div class="stat">
            <div class="stat-value">${graph.statistics.totalServices}</div>
            <div>Total Services</div>
        </div>
        <div class="stat">
            <div class="stat-value">${graph.statistics.singletons}</div>
            <div>Singletons</div>
        </div>
        <div class="stat">
            <div class="stat-value">${graph.statistics.scoped}</div>
            <div>Scoped</div>
        </div>
        <div class="stat">
            <div class="stat-value">${graph.statistics.transient}</div>
            <div>Transient</div>
        </div>
    </div>
    <div class="services">
        ${services}
    </div>
</body>
</html>`;
}

function escapeHtml(text: string): string {
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

export function deactivate() {}
