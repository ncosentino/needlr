# Benchmarks

Performance comparison between source-generated and reflection-based dependency injection.

<div id="key-takeaway" class="admonition tip">
    <p class="admonition-title">Key Takeaway</p>
    <p id="takeaway-text">Source generation provides faster container build times compared to reflection. Service resolution performance is identical once the container is built.</p>
</div>

## Summary {#summary}

<div id="benchmark-summary">
    <p><em>Loading benchmark results...</em></p>
</div>

<div id="benchmark-nav"></div>

<div id="benchmark-sections"></div>

<script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
<script>
document.addEventListener('DOMContentLoaded', async function() {
    const summaryDiv = document.getElementById('benchmark-summary');
    const navDiv = document.getElementById('benchmark-nav');
    const sectionsDiv = document.getElementById('benchmark-sections');
    const takeawayText = document.getElementById('takeaway-text');
    
    try {
        // Load both results and descriptions
        const [resultsRes, descRes] = await Promise.all([
            fetch('../benchmarks/results/results.json'),
            fetch('../benchmarks/results/descriptions.json').catch(() => null)
        ]);
        
        if (!resultsRes.ok) {
            throw new Error('No benchmark data available yet');
        }
        
        const data = await resultsRes.json();
        const descriptions = descRes?.ok ? await descRes.json() : {};
        
        renderSummary(data, summaryDiv);
        renderNavigation(data, descriptions, navDiv);
        renderBenchmarkSections(data, descriptions, sectionsDiv);
        updateKeyTakeaway(data, takeawayText);
    } catch (error) {
        summaryDiv.innerHTML = `
            <div class="admonition info">
                <p class="admonition-title">Benchmarks Not Yet Available</p>
                <p>Benchmark results will appear here after the first benchmark run. 
                   Benchmarks run weekly or can be triggered manually via GitHub Actions.</p>
                <p><a href="https://github.com/ncosentino/needlr/actions/workflows/benchmarks.yml">
                   → Trigger a benchmark run</a></p>
            </div>
        `;
    }
});

function renderSummary(data, container) {
    if (!data?.Benchmarks) {
        container.innerHTML = '<p>No benchmark data found.</p>';
        return;
    }
    
    const totalBenchmarks = data.Benchmarks.length;
    const classes = [...new Set(data.Benchmarks.map(b => b.Type?.split('.').pop()))];
    const hostInfo = data.HostEnvironmentInfo;
    
    let html = `<p><strong>${totalBenchmarks} benchmarks</strong> across <strong>${classes.length} categories</strong></p>`;
    if (hostInfo) {
        html += `<details class="benchmark-env"><summary>Environment Details</summary>`;
        html += `<ul>`;
        html += `<li><strong>OS:</strong> ${hostInfo.OsVersion || 'Unknown'}</li>`;
        html += `<li><strong>CPU:</strong> ${hostInfo.ProcessorName || 'Unknown'}</li>`;
        html += `<li><strong>Runtime:</strong> ${hostInfo.RuntimeVersion || 'Unknown'}</li>`;
        html += `<li><strong>Config:</strong> ${hostInfo.Configuration || 'Unknown'}</li>`;
        html += `</ul></details>`;
    }
    container.innerHTML = html;
}

function renderNavigation(data, descriptions, container) {
    if (!data?.Benchmarks) return;
    
    const classes = [...new Set(data.Benchmarks.map(b => b.Type?.split('.').pop()))].sort();
    
    let html = '<div class="benchmark-toc"><strong>Jump to:</strong> ';
    html += classes.map(c => {
        const anchor = c.toLowerCase().replace(/benchmarks?$/i, '');
        const label = c.replace(/Benchmarks?$/i, '');
        return `<a href="#${anchor}">${label}</a>`;
    }).join(' · ');
    html += '</div>';
    
    container.innerHTML = html;
}

function renderBenchmarkSections(data, descriptions, container) {
    if (!data?.Benchmarks) return;
    
    // Group benchmarks by class
    const grouped = {};
    data.Benchmarks.forEach(b => {
        const className = (b.Type || 'Unknown').split('.').pop();
        if (!grouped[className]) grouped[className] = [];
        grouped[className].push(b);
    });
    
    let html = '';
    let chartIndex = 0;
    
    for (const className of Object.keys(grouped).sort()) {
        const benchmarks = grouped[className];
        const anchor = className.toLowerCase().replace(/benchmarks?$/i, '');
        const displayName = className.replace(/Benchmarks?$/i, '');
        const desc = descriptions[className];
        
        html += `<div class="benchmark-section" id="${anchor}">`;
        html += `<h3>${displayName}</h3>`;
        
        // Description and source link from descriptions.json
        if (desc) {
            html += `<p class="benchmark-desc">${desc.description}</p>`;
            html += `<p class="benchmark-source"><a href="https://github.com/ncosentino/needlr/blob/main/${desc.source}" target="_blank">View Source ↗</a></p>`;
        }
        
        // Data table
        html += renderBenchmarkTable(benchmarks);
        
        // Chart containers
        const baseline = benchmarks.find(b => b.Method?.includes('Reflection'));
        const hasMemory = baseline?.Memory?.BytesAllocatedPerOperation;
        
        html += `<div class="chart-row">`;
        html += `<div class="chart-cell"><canvas id="timeChart${chartIndex}"></canvas></div>`;
        if (hasMemory) {
            html += `<div class="chart-cell"><canvas id="memChart${chartIndex}"></canvas></div>`;
        }
        html += `</div>`;
        
        html += `</div>`;
        chartIndex++;
    }
    
    container.innerHTML = html;
    
    // Render charts after DOM update
    chartIndex = 0;
    for (const className of Object.keys(grouped).sort()) {
        renderChartsForBenchmark(className, grouped[className], chartIndex);
        chartIndex++;
    }
}

function renderBenchmarkTable(benchmarks) {
    const baseline = benchmarks.find(b => b.Method?.includes('Reflection'));
    const baselineMean = getMeanFromMeasurements(baseline);
    const baselineAlloc = baseline?.Memory?.BytesAllocatedPerOperation;
    
    let html = '<table><thead><tr><th>Method</th><th>Mean</th><th>Time Ratio</th><th>Allocated</th><th>Memory Ratio</th></tr></thead><tbody>';
    
    benchmarks.forEach(b => {
        const isBaseline = b.Method?.includes('Reflection');
        const mean = getMeanFromMeasurements(b);
        const allocated = b.Memory?.BytesAllocatedPerOperation;
        
        let timeRatio = '-', timeRatioClass = '';
        if (mean && baselineMean) {
            const ratioVal = mean / baselineMean;
            timeRatio = ratioVal.toFixed(2);
            timeRatioClass = ratioVal < 0.95 ? 'faster' : (ratioVal > 1.05 ? 'slower' : '');
        }
        if (isBaseline) timeRatio = '1.00';
        
        let memRatio = '-', memRatioClass = '';
        if (allocated && baselineAlloc) {
            const ratioVal = allocated / baselineAlloc;
            memRatio = ratioVal.toFixed(2);
            memRatioClass = ratioVal < 0.95 ? 'faster' : (ratioVal > 1.05 ? 'slower' : '');
        }
        if (isBaseline) memRatio = '1.00';
        
        html += `<tr class="${isBaseline ? 'baseline' : ''}">
            <td>${b.Method}</td>
            <td>${formatTime(mean)}</td>
            <td class="${timeRatioClass}">${timeRatio}</td>
            <td>${formatBytes(allocated)}</td>
            <td class="${memRatioClass}">${memRatio}</td>
        </tr>`;
    });
    
    html += '</tbody></table>';
    return html;
}

function renderChartsForBenchmark(className, benchmarks, chartIndex) {
    const baseline = benchmarks.find(b => b.Method?.includes('Reflection'));
    if (!baseline) return;
    
    const methods = benchmarks.map(b => ({
        name: b.Method,
        time: getMeanFromMeasurements(b),
        memory: b.Memory?.BytesAllocatedPerOperation,
        isBaseline: b.Method?.includes('Reflection')
    })).filter(m => m.time);
    
    if (methods.length < 2) return;
    
    const shortClassName = className.replace(/Benchmarks?$/i, '');
    const labels = methods.map(m => m.name.replace(shortClassName + '_', '').replace(/_/g, ' '));
    const timeData = methods.map(m => m.time / 1e6);
    const memData = methods.map(m => m.memory ? m.memory / 1024 : 0);
    const colors = methods.map(m => m.isBaseline ? 'rgba(198, 40, 40, 0.7)' : 'rgba(46, 125, 50, 0.7)');
    const borderColors = methods.map(m => m.isBaseline ? 'rgba(198, 40, 40, 1)' : 'rgba(46, 125, 50, 1)');
    
    // Time chart
    const timeCanvas = document.getElementById(`timeChart${chartIndex}`);
    if (timeCanvas) {
        new Chart(timeCanvas, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{ label: 'Time (ms)', data: timeData, backgroundColor: colors, borderColor: borderColors, borderWidth: 1 }]
            },
            options: {
                responsive: true,
                plugins: { title: { display: true, text: `Time (lower is better)` }, legend: { display: false } },
                scales: { y: { beginAtZero: true, title: { display: true, text: 'ms' } } }
            }
        });
    }
    
    // Memory chart
    const hasMemory = baseline?.Memory?.BytesAllocatedPerOperation;
    if (hasMemory) {
        const memCanvas = document.getElementById(`memChart${chartIndex}`);
        if (memCanvas) {
            new Chart(memCanvas, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{ label: 'Memory (KB)', data: memData, backgroundColor: colors, borderColor: borderColors, borderWidth: 1 }]
                },
                options: {
                    responsive: true,
                    plugins: { title: { display: true, text: `Memory (lower is better)` }, legend: { display: false } },
                    scales: { y: { beginAtZero: true, title: { display: true, text: 'KB' } } }
                }
            });
        }
    }
}

function getMeanFromMeasurements(benchmark) {
    if (!benchmark?.Measurements) return null;
    if (benchmark.Statistics?.Mean) return benchmark.Statistics.Mean;
    
    const actuals = benchmark.Measurements.filter(m => 
        m.IterationMode === 'Workload' && m.IterationStage === 'Actual'
    );
    if (actuals.length === 0) return null;
    
    const totalNsPerOp = actuals.reduce((sum, m) => sum + (m.Nanoseconds / m.Operations), 0);
    return totalNsPerOp / actuals.length;
}

function formatTime(ns) {
    if (!ns) return '-';
    if (ns >= 1e9) return (ns / 1e9).toFixed(2) + ' s';
    if (ns >= 1e6) return (ns / 1e6).toFixed(2) + ' ms';
    if (ns >= 1e3) return (ns / 1e3).toFixed(2) + ' μs';
    return ns.toFixed(2) + ' ns';
}

function formatBytes(bytes) {
    if (!bytes) return '-';
    if (bytes >= 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
    if (bytes >= 1024) return (bytes / 1024).toFixed(2) + ' KB';
    return bytes + ' B';
}

function updateKeyTakeaway(data, element) {
    if (!data?.Benchmarks || !element) return;
    
    const buildBenchmarks = data.Benchmarks.filter(b => 
        b.Type?.includes('Build') || b.Method?.includes('Build')
    );
    
    let totalSpeedup = 0, count = 0;
    const grouped = {};
    buildBenchmarks.forEach(b => {
        const className = (b.Type || 'Unknown').split('.').pop();
        if (!grouped[className]) grouped[className] = {};
        if (b.Method?.includes('Reflection')) grouped[className].reflection = b;
        else if (b.Method?.includes('SourceGen') && !b.Method?.includes('Explicit')) grouped[className].sourceGen = b;
    });
    
    for (const benchmarks of Object.values(grouped)) {
        if (benchmarks.reflection && benchmarks.sourceGen) {
            const refTime = getMeanFromMeasurements(benchmarks.reflection);
            const sgTime = getMeanFromMeasurements(benchmarks.sourceGen);
            if (refTime && sgTime && sgTime > 0) {
                totalSpeedup += refTime / sgTime;
                count++;
            }
        }
    }
    
    if (count > 0) {
        const avgSpeedup = (totalSpeedup / count).toFixed(1);
        element.innerHTML = `<strong>Build time is ~${avgSpeedup}x faster with source generation.</strong> Service resolution is identical once the container is built.`;
    }
}
</script>

<style>
.benchmark-toc {
    background: var(--md-default-fg-color--lightest);
    padding: 0.75em 1em;
    border-radius: 4px;
    margin: 1em 0 2em 0;
}
.benchmark-toc a {
    text-decoration: none;
}
.benchmark-section {
    margin: 2em 0;
    padding: 1.5em;
    border: 1px solid var(--md-default-fg-color--lightest);
    border-radius: 8px;
}
.benchmark-section h3 {
    margin-top: 0;
}
.benchmark-desc {
    color: var(--md-default-fg-color--light);
    margin-bottom: 0.5em;
}
.benchmark-source {
    font-size: 0.85em;
    margin-bottom: 1em;
}
.benchmark-section table {
    width: 100%;
    border-collapse: collapse;
    margin: 1em 0;
}
.benchmark-section th,
.benchmark-section td {
    padding: 8px 12px;
    text-align: left;
    border-bottom: 1px solid var(--md-default-fg-color--lightest);
}
.benchmark-section th {
    background: var(--md-default-fg-color--lightest);
    font-weight: 600;
}
.benchmark-section .baseline {
    background: rgba(198, 40, 40, 0.1);
}
.benchmark-section .faster {
    color: #2e7d32;
    font-weight: 600;
}
.benchmark-section .slower {
    color: #c62828;
}
.chart-row {
    display: flex;
    flex-wrap: wrap;
    gap: 1em;
    margin-top: 1em;
}
.chart-cell {
    flex: 1;
    min-width: 280px;
    max-height: 300px;
}
.benchmark-env {
    margin-top: 0.5em;
}
.benchmark-env summary {
    cursor: pointer;
    color: var(--md-default-fg-color--light);
}
</style>

## Running Benchmarks Locally

```bash
cd src/NexusLabs.Needlr.Benchmarks
dotnet run -c Release -- --filter '*Build*'
```

To run all benchmarks:

```bash
dotnet run -c Release -- --filter '*'
```

## CI Integration

Benchmarks run automatically:

- **Weekly**: Every Sunday at 3am UTC (if code has changed)
- **On-demand**: Via [workflow dispatch](https://github.com/ncosentino/needlr/actions/workflows/benchmarks.yml)

Results are published to this page after each run.

## Methodology

All benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) with:

- **ShortRun job**: 3 warmup iterations, 3 target iterations
- **Memory diagnostics**: Tracks allocations per operation
- **Baseline comparison**: Reflection is always the baseline

Each benchmark class follows strict rules:

1. One baseline per class (reflection approach)
2. All methods in a class compare the same scenario
3. Benchmark methods contain only what needs to be measured
