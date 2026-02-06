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
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const graphLoader_1 = require("./graphLoader");
const servicesTreeProvider_1 = require("./servicesTreeProvider");
const codeLensProvider_1 = require("./codeLensProvider");
let graphLoader;
let servicesTreeProvider;
async function activate(context) {
    console.log('Needlr Tools extension is now active');
    // Initialize graph loader
    graphLoader = new graphLoader_1.GraphLoader(context);
    context.subscriptions.push(graphLoader);
    // Initialize services tree view
    servicesTreeProvider = new servicesTreeProvider_1.ServicesTreeProvider(graphLoader);
    const treeView = vscode.window.createTreeView('needlrServices', {
        treeDataProvider: servicesTreeProvider,
        showCollapseAll: true
    });
    context.subscriptions.push(treeView);
    // Initialize CodeLens provider
    const codeLensProvider = new codeLensProvider_1.NeedlrCodeLensProvider(graphLoader);
    context.subscriptions.push(vscode.languages.registerCodeLensProvider({ language: 'csharp', scheme: 'file' }, codeLensProvider));
    // Register commands
    context.subscriptions.push(vscode.commands.registerCommand('needlr.showDependencyGraph', showDependencyGraph), vscode.commands.registerCommand('needlr.refreshGraph', refreshGraph), vscode.commands.registerCommand('needlr.goToService', goToService), vscode.commands.registerCommand('needlr.filterServices', filterServices), vscode.commands.registerCommand('needlr.clearFilter', () => servicesTreeProvider.clearFilter()), vscode.commands.registerCommand('needlr.showServiceDetails', showServiceDetails), vscode.commands.registerCommand('needlr.navigateToLocation', navigateToLocation));
    // Initialize the graph loader
    await graphLoader.initialize();
    // Show welcome message if no graph found
    const graph = graphLoader.getGraph();
    if (!graph) {
        vscode.window.showInformationMessage('Needlr Tools: No dependency graph found. Build your project to generate one.', 'Learn More').then(selection => {
            if (selection === 'Learn More') {
                vscode.env.openExternal(vscode.Uri.parse('https://github.com/ncosentino/needlr'));
            }
        });
    }
}
async function filterServices() {
    const filter = await vscode.window.showInputBox({
        prompt: 'Filter services by name, interface, or dependency',
        placeHolder: 'Enter search term...'
    });
    if (filter !== undefined) {
        servicesTreeProvider.setFilter(filter);
    }
}
async function showServiceDetails(service) {
    const graph = graphLoader.getGraph();
    if (!graph)
        return;
    // Find services that use this service
    const usedBy = graph.services.filter(s => s.dependencies.some(d => d.fullTypeName === service.fullTypeName ||
        service.interfaces.some(i => i.fullName === d.fullTypeName)));
    const items = [];
    // Dependencies section
    if (service.dependencies.length > 0) {
        items.push({ label: '$(arrow-right) Dependencies', kind: vscode.QuickPickItemKind.Separator });
        for (const dep of service.dependencies) {
            const resolvedService = graph.services.find(s => s.fullTypeName === dep.resolvedTo ||
                s.fullTypeName === dep.fullTypeName ||
                s.interfaces.some(i => i.fullName === dep.fullTypeName));
            // Check if this dependency is an interface - find its location from any service that implements it
            let interfaceLocation;
            for (const s of graph.services) {
                const iface = s.interfaces.find(i => i.fullName === dep.fullTypeName);
                if (iface?.location?.filePath && iface.location.line > 0) {
                    interfaceLocation = iface.location;
                    break;
                }
            }
            const hasInterfaceLocation = interfaceLocation?.filePath && interfaceLocation.line > 0;
            const hasServiceLocation = resolvedService?.location?.filePath && resolvedService.location.line > 0;
            items.push({
                label: `  ${dep.typeName}`,
                description: dep.resolvedTo ? `→ ${getSimpleTypeName(dep.resolvedTo)}` : undefined,
                detail: hasInterfaceLocation ? 'Click to navigate to interface' : (hasServiceLocation ? 'Click to navigate' : '(No source location)'),
                targetService: resolvedService,
                interfaceLocation: interfaceLocation
            });
        }
    }
    // Used by section
    if (usedBy.length > 0) {
        items.push({ label: '$(arrow-left) Used By', kind: vscode.QuickPickItemKind.Separator });
        for (const consumer of usedBy) {
            const hasLocation = consumer.location?.filePath && consumer.location.line > 0;
            items.push({
                label: `  ${consumer.typeName}`,
                description: consumer.lifetime,
                detail: hasLocation ? 'Click to navigate' : '(No source location)',
                targetService: consumer
            });
        }
    }
    // Interfaces section - with navigation when location available
    if (service.interfaces.length > 0) {
        items.push({ label: '$(symbol-interface) Interfaces', kind: vscode.QuickPickItemKind.Separator });
        for (const iface of service.interfaces) {
            const hasLocation = iface.location?.filePath && iface.location.line > 0;
            items.push({
                label: `  ${iface.name}`,
                description: iface.fullName,
                detail: hasLocation ? 'Click to navigate' : '(No source location)',
                interfaceLocation: hasLocation ? iface.location : undefined
            });
        }
    }
    const selected = await vscode.window.showQuickPick(items, {
        title: `${service.typeName} - ${service.lifetime}`,
        placeHolder: 'Select to navigate'
    });
    if (selected && selected.kind !== vscode.QuickPickItemKind.Separator) {
        // Handle interface navigation
        if (selected.interfaceLocation?.filePath && selected.interfaceLocation.line > 0) {
            const uri = vscode.Uri.file(selected.interfaceLocation.filePath);
            const position = new vscode.Position(selected.interfaceLocation.line - 1, 0);
            const document = await vscode.workspace.openTextDocument(uri);
            const editor = await vscode.window.showTextDocument(document);
            editor.selection = new vscode.Selection(position, position);
            editor.revealRange(new vscode.Range(position, position), vscode.TextEditorRevealType.InCenter);
        }
        // Handle service navigation
        else if (selected.targetService?.location?.filePath && selected.targetService.location.line > 0) {
            const uri = vscode.Uri.file(selected.targetService.location.filePath);
            const position = new vscode.Position(selected.targetService.location.line - 1, 0);
            const document = await vscode.workspace.openTextDocument(uri);
            const editor = await vscode.window.showTextDocument(document);
            editor.selection = new vscode.Selection(position, position);
            editor.revealRange(new vscode.Range(position, position), vscode.TextEditorRevealType.InCenter);
        }
    }
}
async function navigateToLocation(filePath, line) {
    const uri = vscode.Uri.file(filePath);
    const position = new vscode.Position(line - 1, 0);
    const document = await vscode.workspace.openTextDocument(uri);
    const editor = await vscode.window.showTextDocument(document);
    editor.selection = new vscode.Selection(position, position);
    editor.revealRange(new vscode.Range(position, position), vscode.TextEditorRevealType.InCenter);
}
function getSimpleTypeName(fullTypeName) {
    const lastDot = fullTypeName.lastIndexOf('.');
    return lastDot >= 0 ? fullTypeName.substring(lastDot + 1) : fullTypeName;
}
async function showDependencyGraph() {
    const graph = graphLoader.getGraph();
    if (!graph) {
        vscode.window.showWarningMessage('No Needlr graph available. Build your project first.');
        return;
    }
    // Create and show a webview panel with the dependency graph
    const panel = vscode.window.createWebviewPanel('needlrGraph', 'Needlr Dependency Graph', vscode.ViewColumn.One, {
        enableScripts: true
    });
    panel.webview.html = generateGraphHtml(graph);
}
async function refreshGraph() {
    const graph = await graphLoader.findAndLoadGraph();
    if (graph) {
        servicesTreeProvider.refresh();
        vscode.window.showInformationMessage(`Needlr: Loaded ${graph.statistics.totalServices} services`);
    }
    else {
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
        const position = new vscode.Position(selected.service.location.line - 1, 0);
        const document = await vscode.workspace.openTextDocument(uri);
        const editor = await vscode.window.showTextDocument(document);
        editor.selection = new vscode.Selection(position, position);
        editor.revealRange(new vscode.Range(position, position), vscode.TextEditorRevealType.InCenter);
    }
}
function generateGraphHtml(graph) {
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
function escapeHtml(text) {
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}
function deactivate() { }
//# sourceMappingURL=extension.js.map