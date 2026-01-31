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

<div id="charts-container">
    <canvas id="buildChart" style="max-height: 400px;"></canvas>
</div>

<script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
<script>
document.addEventListener('DOMContentLoaded', async function() {
    const resultsDiv = document.getElementById('benchmark-results');
    const chartCanvas = document.getElementById('buildChart');
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
        renderChart(data, chartCanvas);
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
        chartCanvas.style.display = 'none';
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
        html += '<table><thead><tr><th>Method</th><th>Mean</th><th>Allocated</th><th>Ratio</th></tr></thead><tbody>';
        
        const baseline = benchmarks.find(b => b.Baseline);
        benchmarks.forEach(b => {
            const isBaseline = b.Baseline;
            const mean = formatTime(b.Statistics?.Mean);
            const allocated = formatBytes(b.Memory?.BytesAllocatedPerOperation);
            const ratio = isBaseline ? '1.00' : (b.Statistics?.Mean / baseline?.Statistics?.Mean).toFixed(2);
            const ratioClass = ratio < 1 ? 'faster' : (ratio > 1 ? 'slower' : '');
            const rowClass = isBaseline ? 'baseline' : '';
            
            html += `<tr class="${rowClass}">
                <td>${b.Method}</td>
                <td>${mean}</td>
                <td>${allocated}</td>
                <td class="${ratioClass}">${ratio}</td>
            </tr>`;
        });
        html += '</tbody></table>';
    }
    
    container.innerHTML = html;
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

function renderChart(data, canvas) {
    if (!data || !data.Benchmarks) {
        canvas.style.display = 'none';
        return;
    }
    
    // Filter to build benchmarks only
    const buildBenchmarks = data.Benchmarks.filter(b => 
        b.Type?.includes('Build') || b.Method?.includes('Build')
    );
    
    if (buildBenchmarks.length === 0) {
        canvas.style.display = 'none';
        return;
    }
    
    // Group by benchmark class
    const grouped = {};
    buildBenchmarks.forEach(b => {
        const className = (b.Type || 'Unknown').split('.').pop();
        if (!grouped[className]) grouped[className] = { labels: [], reflection: 0, sourceGen: 0 };
        
        const meanMs = (b.Statistics?.Mean || 0) / 1e6;
        if (b.Method?.includes('Reflection')) {
            grouped[className].reflection = meanMs;
        } else if (b.Method?.includes('SourceGen') && !b.Method?.includes('Explicit')) {
            grouped[className].sourceGen = meanMs;
        }
    });
    
    const labels = Object.keys(grouped);
    const reflectionData = labels.map(l => grouped[l].reflection);
    const sourceGenData = labels.map(l => grouped[l].sourceGen);
    
    new Chart(canvas, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'Reflection (Baseline)',
                    data: reflectionData,
                    backgroundColor: 'rgba(198, 40, 40, 0.7)',
                    borderColor: 'rgba(198, 40, 40, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Source Generation',
                    data: sourceGenData,
                    backgroundColor: 'rgba(46, 125, 50, 0.7)',
                    borderColor: 'rgba(46, 125, 50, 1)',
                    borderWidth: 1
                }
            ]
        },
        options: {
            responsive: true,
            plugins: {
                title: {
                    display: true,
                    text: 'Build Time Comparison (lower is better)'
                },
                legend: {
                    position: 'top'
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Time (ms)'
                    }
                }
            }
        }
    });
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
            const refTime = benchmarks.reflection.Statistics?.Mean || 0;
            const sgTime = benchmarks.sourceGen.Statistics?.Mean || 0;
            const refMem = benchmarks.reflection.Memory?.BytesAllocatedPerOperation || 0;
            const sgMem = benchmarks.sourceGen.Memory?.BytesAllocatedPerOperation || 0;
            
            if (sgTime > 0) {
                totalSpeedup += refTime / sgTime;
                count++;
            }
            if (refMem > 0) {
                totalMemorySaving += (1 - sgMem / refMem) * 100;
            }
        }
    }
    
    if (count > 0) {
        const avgSpeedup = (totalSpeedup / count).toFixed(1);
        const avgMemorySaving = Math.round(totalMemorySaving / count);
        element.innerHTML = `<strong>Host/WebApplication build time is ~${avgSpeedup}x faster with source generation</strong>, with ~${avgMemorySaving}% less memory allocation. Service resolution performance is identical once the container is built.`;
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
