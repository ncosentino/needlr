# Benchmarks

Performance comparison between source-generated and reflection-based dependency injection.

<div id="key-takeaway" class="admonition tip">
    <p class="admonition-title">Key Takeaway</p>
    <p id="takeaway-text">Source generation provides faster container build times compared to reflection. Service resolution performance is identical once the container is built.</p>
</div>

## Results

<div id="benchmark-results">
    <p><em>Loading benchmark results...</em></p>
</div>

<div id="charts-container"></div>

<script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
<script>
document.addEventListener('DOMContentLoaded', async function() {
    const resultsDiv = document.getElementById('benchmark-results');
    const chartsContainer = document.getElementById('charts-container');
    const takeawayText = document.getElementById('takeaway-text');
    
    // Try to load benchmark data from the benchmarks directory
    try {
        // The benchmark workflow exports results to /benchmarks/
        const response = await fetch('../benchmarks/results/results.json');
        if (!response.ok) {
            throw new Error('No benchmark data available yet');
        }
        
        const data = await response.json();
        displayResults(data, resultsDiv);
        renderAllCharts(data, chartsContainer);
        updateKeyTakeaway(data, takeawayText);
    } catch (error) {
        resultsDiv.innerHTML = `
            <div class="admonition info">
                <p class="admonition-title">Benchmarks Not Yet Available</p>
                <p>Benchmark results will appear here after the first benchmark run. 
                   Benchmarks run weekly or can be triggered manually via GitHub Actions.</p>
                <p><a href="https://github.com/ncosentino/needlr/actions/workflows/benchmarks.yml">
                   → Trigger a benchmark run</a></p>
            </div>
        `;
        chartsContainer.style.display = 'none';
    }
});

function displayResults(data, container) {
    if (!data || !data.Benchmarks) {
        container.innerHTML = '<p>No benchmark data found.</p>';
        return;
    }
    
    // Group benchmarks by class
    const grouped = {};
    data.Benchmarks.forEach(b => {
        const className = b.Type || 'Unknown';
        if (!grouped[className]) grouped[className] = [];
        grouped[className].push(b);
    });
    
    let html = '';
    for (const [className, benchmarks] of Object.entries(grouped)) {
        const displayName = className.split('.').pop();
        html += `<h3>${displayName}</h3>`;
        html += '<table><thead><tr><th>Method</th><th>Mean</th><th>Time Ratio</th><th>Allocated</th><th>Memory Ratio</th></tr></thead><tbody>';
        
        // Find baseline (method containing "Reflection")
        const baseline = benchmarks.find(b => b.Method?.includes('Reflection'));
        const baselineMean = getMeanFromMeasurements(baseline);
        const baselineAlloc = baseline?.Memory?.BytesAllocatedPerOperation;
        
        benchmarks.forEach(b => {
            const isBaseline = b.Method?.includes('Reflection');
            const mean = getMeanFromMeasurements(b);
            const allocated = b.Memory?.BytesAllocatedPerOperation;
            
            // Time ratio
            let timeRatio = '-';
            let timeRatioClass = '';
            if (mean && baselineMean) {
                const ratioVal = mean / baselineMean;
                timeRatio = ratioVal.toFixed(2);
                timeRatioClass = ratioVal < 0.95 ? 'faster' : (ratioVal > 1.05 ? 'slower' : '');
            }
            if (isBaseline) timeRatio = '1.00';
            
            // Memory ratio
            let memRatio = '-';
            let memRatioClass = '';
            if (allocated && baselineAlloc) {
                const ratioVal = allocated / baselineAlloc;
                memRatio = ratioVal.toFixed(2);
                memRatioClass = ratioVal < 0.95 ? 'faster' : (ratioVal > 1.05 ? 'slower' : '');
            }
            if (isBaseline) memRatio = '1.00';
            
            const rowClass = isBaseline ? 'baseline' : '';
            
            html += `<tr class="${rowClass}">
                <td>${b.Method}</td>
                <td>${formatTime(mean)}</td>
                <td class="${timeRatioClass}">${timeRatio}</td>
                <td>${formatBytes(allocated)}</td>
                <td class="${memRatioClass}">${memRatio}</td>
            </tr>`;
        });
        html += '</tbody></table>';
    }
    
    container.innerHTML = html;
}

function getMeanFromMeasurements(benchmark) {
    if (!benchmark?.Measurements) return null;
    
    // Use Statistics.Mean if available
    if (benchmark.Statistics?.Mean) return benchmark.Statistics.Mean;
    
    // Calculate from Actual workload measurements
    const actuals = benchmark.Measurements.filter(m => 
        m.IterationMode === 'Workload' && m.IterationStage === 'Actual'
    );
    
    if (actuals.length === 0) return null;
    
    // Calculate mean ns per operation
    const totalNsPerOp = actuals.reduce((sum, m) => {
        return sum + (m.Nanoseconds / m.Operations);
    }, 0);
    
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

function renderAllCharts(data, container) {
    if (!data || !data.Benchmarks) {
        container.style.display = 'none';
        return;
    }
    
    // Group benchmarks by class
    const grouped = {};
    data.Benchmarks.forEach(b => {
        const className = (b.Type || 'Unknown').split('.').pop();
        if (!grouped[className]) grouped[className] = [];
        grouped[className].push(b);
    });
    
    let html = '';
    let chartIndex = 0;
    
    for (const [className, benchmarks] of Object.entries(grouped)) {
        // Find baseline and collect data
        const baseline = benchmarks.find(b => b.Method?.includes('Reflection'));
        if (!baseline) continue;
        
        const baselineAlloc = baseline?.Memory?.BytesAllocatedPerOperation;
        
        // Collect all methods with their data
        const methods = benchmarks.map(b => ({
            name: b.Method,
            time: getMeanFromMeasurements(b),
            memory: b.Memory?.BytesAllocatedPerOperation,
            isBaseline: b.Method?.includes('Reflection')
        })).filter(m => m.time);
        
        if (methods.length < 2) continue;
        
        // Create chart containers
        html += `<div class="benchmark-chart-group">`;
        html += `<h4>${className.replace('Benchmarks', '')}</h4>`;
        html += `<div class="chart-row">`;
        html += `<div class="chart-cell"><canvas id="timeChart${chartIndex}"></canvas></div>`;
        if (baselineAlloc) {
            html += `<div class="chart-cell"><canvas id="memChart${chartIndex}"></canvas></div>`;
        }
        html += `</div></div>`;
        chartIndex++;
    }
    
    container.innerHTML = html;
    
    // Now render charts
    chartIndex = 0;
    for (const [className, benchmarks] of Object.entries(grouped)) {
        const baseline = benchmarks.find(b => b.Method?.includes('Reflection'));
        if (!baseline) continue;
        
        const baselineAlloc = baseline?.Memory?.BytesAllocatedPerOperation;
        
        const methods = benchmarks.map(b => ({
            name: b.Method,
            time: getMeanFromMeasurements(b),
            memory: b.Memory?.BytesAllocatedPerOperation,
            isBaseline: b.Method?.includes('Reflection')
        })).filter(m => m.time);
        
        if (methods.length < 2) continue;
        
        // Shorten method names for labels
        const shortClassName = className.replace('Benchmarks', '');
        const labels = methods.map(m => m.name.replace(shortClassName + '_', '').replace('_', ' '));
        const timeData = methods.map(m => m.time / 1e6); // Convert to ms
        const memData = methods.map(m => m.memory ? m.memory / 1024 : 0); // Convert to KB
        const colors = methods.map(m => m.isBaseline ? 'rgba(198, 40, 40, 0.7)' : 'rgba(46, 125, 50, 0.7)');
        const borderColors = methods.map(m => m.isBaseline ? 'rgba(198, 40, 40, 1)' : 'rgba(46, 125, 50, 1)');
        
        // Time chart
        const timeCanvas = document.getElementById(`timeChart${chartIndex}`);
        if (timeCanvas) {
            new Chart(timeCanvas, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Time (ms)',
                        data: timeData,
                        backgroundColor: colors,
                        borderColor: borderColors,
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        title: { display: true, text: `${shortClassName} - Time (lower is better)` },
                        legend: { display: false }
                    },
                    scales: {
                        y: { beginAtZero: true, title: { display: true, text: 'ms' } }
                    }
                }
            });
        }
        
        // Memory chart
        if (baselineAlloc) {
            const memCanvas = document.getElementById(`memChart${chartIndex}`);
            if (memCanvas) {
                new Chart(memCanvas, {
                    type: 'bar',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Memory (KB)',
                            data: memData,
                            backgroundColor: colors,
                            borderColor: borderColors,
                            borderWidth: 1
                        }]
                    },
                    options: {
                        responsive: true,
                        plugins: {
                            title: { display: true, text: `${shortClassName} - Memory (lower is better)` },
                            legend: { display: false }
                        },
                        scales: {
                            y: { beginAtZero: true, title: { display: true, text: 'KB' } }
                        }
                    }
                });
            }
        }
        
        chartIndex++;
    }
}

function updateKeyTakeaway(data, element) {
    if (!data || !data.Benchmarks || !element) return;
    
    // Find build benchmarks and calculate average speedup
    const buildBenchmarks = data.Benchmarks.filter(b => 
        b.Type?.includes('Build') || b.Method?.includes('Build')
    );
    
    let totalSpeedup = 0;
    let totalMemorySaving = 0;
    let count = 0;
    let memCount = 0;
    
    // Group by class to find reflection/sourcegen pairs
    const grouped = {};
    buildBenchmarks.forEach(b => {
        const className = (b.Type || 'Unknown').split('.').pop();
        if (!grouped[className]) grouped[className] = {};
        
        if (b.Method?.includes('Reflection')) {
            grouped[className].reflection = b;
        } else if (b.Method?.includes('SourceGen') && !b.Method?.includes('Explicit')) {
            grouped[className].sourceGen = b;
        }
    });
    
    for (const benchmarks of Object.values(grouped)) {
        if (benchmarks.reflection && benchmarks.sourceGen) {
            const refTime = getMeanFromMeasurements(benchmarks.reflection);
            const sgTime = getMeanFromMeasurements(benchmarks.sourceGen);
            const refMem = benchmarks.reflection.Memory?.BytesAllocatedPerOperation || 0;
            const sgMem = benchmarks.sourceGen.Memory?.BytesAllocatedPerOperation || 0;
            
            if (refTime && sgTime && sgTime > 0) {
                totalSpeedup += refTime / sgTime;
                count++;
            }
            if (refMem > 0 && sgMem > 0) {
                totalMemorySaving += (1 - sgMem / refMem) * 100;
                memCount++;
            }
        }
    }
    
    if (count > 0) {
        const avgSpeedup = (totalSpeedup / count).toFixed(1);
        let text = `<strong>Build time is ~${avgSpeedup}x faster with source generation</strong>`;
        if (memCount > 0) {
            const avgMemorySaving = Math.round(totalMemorySaving / memCount);
            text += `, with ~${avgMemorySaving}% less memory allocation`;
        }
        text += `. Service resolution is identical once the container is built.`;
        element.innerHTML = text;
    }
}
</script>

<style>
#benchmark-results table {
    width: 100%;
    border-collapse: collapse;
    margin: 1em 0;
}
#benchmark-results th,
#benchmark-results td {
    padding: 8px 12px;
    text-align: left;
    border-bottom: 1px solid var(--md-default-fg-color--lightest);
}
#benchmark-results th {
    background: var(--md-default-fg-color--lightest);
    font-weight: 600;
}
#benchmark-results .baseline {
    background: rgba(46, 125, 50, 0.1);
}
#benchmark-results .faster {
    color: #2e7d32;
    font-weight: 600;
}
#benchmark-results .slower {
    color: #c62828;
}
.benchmark-chart-group {
    margin: 2em 0;
    padding: 1em;
    border: 1px solid var(--md-default-fg-color--lightest);
    border-radius: 8px;
}
.benchmark-chart-group h4 {
    margin-top: 0;
    margin-bottom: 1em;
}
.chart-row {
    display: flex;
    flex-wrap: wrap;
    gap: 1em;
}
.chart-cell {
    flex: 1;
    min-width: 300px;
    max-height: 300px;
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
