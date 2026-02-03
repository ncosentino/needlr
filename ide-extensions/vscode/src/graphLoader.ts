import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { NeedlrGraph } from './types';

/**
 * Watches for and loads Needlr graph files from the workspace.
 */
export class GraphLoader implements vscode.Disposable {
    private readonly _onGraphLoaded = new vscode.EventEmitter<NeedlrGraph>();
    public readonly onGraphLoaded = this._onGraphLoaded.event;

    private readonly _onGraphCleared = new vscode.EventEmitter<void>();
    public readonly onGraphCleared = this._onGraphCleared.event;

    private fileWatcher: vscode.FileSystemWatcher | undefined;
    private currentGraph: NeedlrGraph | undefined;

    constructor(private readonly context: vscode.ExtensionContext) {}

    public async initialize(): Promise<void> {
        // Set up file watcher for graph files
        const config = vscode.workspace.getConfiguration('needlr');
        const pattern = config.get<string>('graphFilePath', '**/obj/**/needlr-graph.json');
        
        this.fileWatcher = vscode.workspace.createFileSystemWatcher(pattern);
        
        this.fileWatcher.onDidCreate(uri => this.loadGraphFile(uri));
        this.fileWatcher.onDidChange(uri => this.loadGraphFile(uri));
        this.fileWatcher.onDidDelete(() => this.clearGraph());

        // Initial load
        await this.findAndLoadGraph();
    }

    public async findAndLoadGraph(): Promise<NeedlrGraph | undefined> {
        const config = vscode.workspace.getConfiguration('needlr');
        const pattern = config.get<string>('graphFilePath', '**/obj/**/needlr-graph.json');
        
        const files = await vscode.workspace.findFiles(pattern, '**/node_modules/**', 1);
        
        if (files.length > 0) {
            return this.loadGraphFile(files[0]);
        }

        // Also check for embedded graph in generated source files
        const sourceFiles = await vscode.workspace.findFiles('**/NeedlrGraph.g.cs', '**/node_modules/**', 1);
        if (sourceFiles.length > 0) {
            return this.loadGraphFromSourceFile(sourceFiles[0]);
        }

        return undefined;
    }

    private async loadGraphFile(uri: vscode.Uri): Promise<NeedlrGraph | undefined> {
        try {
            const content = await fs.promises.readFile(uri.fsPath, 'utf-8');
            const graph = JSON.parse(content) as NeedlrGraph;
            
            this.currentGraph = graph;
            this._onGraphLoaded.fire(graph);
            
            await vscode.commands.executeCommand('setContext', 'needlr:hasGraph', true);
            
            return graph;
        } catch (error) {
            console.error('Failed to load Needlr graph:', error);
            return undefined;
        }
    }

    private async loadGraphFromSourceFile(uri: vscode.Uri): Promise<NeedlrGraph | undefined> {
        try {
            const content = await fs.promises.readFile(uri.fsPath, 'utf-8');
            
            // Extract JSON from the C# source file
            // The JSON is embedded as a string constant in the generated code
            const jsonMatch = content.match(/GraphJson\s*=\s*@?"([^"]+(?:""[^"]*)*)"(?:;|$)/s);
            if (!jsonMatch) {
                // Try multi-line raw string literal format
                const rawMatch = content.match(/GraphJson\s*=\s*"""([\s\S]*?)"""/);
                if (rawMatch) {
                    const graph = JSON.parse(rawMatch[1]) as NeedlrGraph;
                    this.currentGraph = graph;
                    this._onGraphLoaded.fire(graph);
                    await vscode.commands.executeCommand('setContext', 'needlr:hasGraph', true);
                    return graph;
                }
                return undefined;
            }

            // Unescape the C# string
            const jsonString = jsonMatch[1].replace(/""/g, '"');
            const graph = JSON.parse(jsonString) as NeedlrGraph;
            
            this.currentGraph = graph;
            this._onGraphLoaded.fire(graph);
            
            await vscode.commands.executeCommand('setContext', 'needlr:hasGraph', true);
            
            return graph;
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
        this.fileWatcher?.dispose();
        this._onGraphLoaded.dispose();
        this._onGraphCleared.dispose();
    }
}
