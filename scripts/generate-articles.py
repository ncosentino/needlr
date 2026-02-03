#!/usr/bin/env python3
"""
Generate articles.md from Dev Leader RSS feed filtered by Needlr tag.
Usage: python scripts/generate-articles.py docs/articles.md
"""

import sys
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime


def fetch_rss_feed(url: str) -> str:
    """Fetch RSS feed content from URL."""
    with urllib.request.urlopen(url, timeout=30) as response:
        return response.read().decode('utf-8')


def parse_articles(xml_content: str, tag_filter: str) -> list[dict]:
    """Parse RSS feed and filter articles by tag."""
    root = ET.fromstring(xml_content)
    articles = []
    
    for item in root.findall('.//item'):
        # Check if article has the Needlr tag
        categories = [cat.text for cat in item.findall('category') if cat.text]
        if tag_filter not in categories:
            continue
        
        title = item.find('title')
        link = item.find('link')
        description = item.find('description')
        pub_date = item.find('pubDate')
        
        # Parse media image
        media_ns = {'media': 'http://search.yahoo.com/mrss/'}
        media = item.find('media:content', media_ns)
        image_url = media.get('url') if media is not None else None
        
        if title is not None and link is not None:
            # Parse date
            date_str = ""
            if pub_date is not None and pub_date.text:
                try:
                    dt = datetime.strptime(pub_date.text, "%a, %d %b %Y %H:%M:%S %z")
                    date_str = dt.strftime("%B %d, %Y")
                except ValueError:
                    date_str = pub_date.text
            
            # Clean description
            desc = ""
            if description is not None and description.text:
                desc = description.text.strip()
                # Remove CDATA wrapper if present
                if desc.startswith('<![CDATA['):
                    desc = desc[9:]
                if desc.endswith(']]>'):
                    desc = desc[:-3]
                # Remove HTML tags
                import re
                desc = re.sub(r'<[^>]+>', '', desc).strip()
            
            articles.append({
                'title': title.text,
                'link': link.text,
                'description': desc,
                'date': date_str,
                'image': image_url,
                'categories': [c for c in categories if c != tag_filter]
            })
    
    return articles


def generate_markdown(articles: list[dict]) -> str:
    """Generate markdown content from articles."""
    lines = [
        "# Articles & Blog Posts",
        "",
        "Blog posts and articles about Needlr from [Dev Leader](https://devleader.ca).",
        "",
        "!!! tip \"Stay Updated\"",
        "    Follow the [Needlr tag on Dev Leader](https://devleader.ca/tags/Needlr) for the latest articles.",
        "",
    ]
    
    if not articles:
        lines.extend([
            "No articles found yet. Check back soon!",
            "",
        ])
    else:
        for article in articles:
            # Featured image as clickable link
            if article.get('image'):
                lines.append(f"[![{article['title']}]({article['image']})]({article['link']})")
                lines.append("")
            
            lines.append(f"## [{article['title']}]({article['link']})")
            lines.append("")
            if article['date']:
                lines.append(f"*Published: {article['date']}*")
                lines.append("")
            if article['description']:
                lines.append(f"> {article['description']}")
                lines.append("")
            if article['categories']:
                tags = ", ".join(f"`{c}`" for c in article['categories'][:5])
                lines.append(f"**Tags:** {tags}")
                lines.append("")
            lines.append("---")
            lines.append("")
    
    # Add footer
    lines.extend([
        "",
        "*This page is automatically generated from the Dev Leader RSS feed.*",
    ])
    
    return "\n".join(lines)


def main():
    if len(sys.argv) < 2:
        print("Usage: python generate-articles.py <output-file>")
        sys.exit(1)
    
    output_file = sys.argv[1]
    rss_url = "https://devleader.ca/feed"
    tag_filter = "Needlr"
    
    print(f"Fetching articles from {rss_url}...")
    
    try:
        xml_content = fetch_rss_feed(rss_url)
        articles = parse_articles(xml_content, tag_filter)
        print(f"Found {len(articles)} articles tagged with '{tag_filter}'")
        
        markdown = generate_markdown(articles)
        
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(markdown)
        
        print(f"Generated {output_file}")
        
    except Exception as e:
        print(f"Error: {e}")
        # Create a fallback page
        fallback = """# Articles & Blog Posts

Blog posts and articles about Needlr from [Dev Leader](https://devleader.ca).

!!! tip "Stay Updated"
    Follow the [Needlr tag on Dev Leader](https://devleader.ca/tags/Needlr) for the latest articles.

*Unable to fetch articles at build time. Visit [Dev Leader](https://devleader.ca/tags/Needlr) directly.*
"""
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(fallback)
        print(f"Created fallback {output_file}")


if __name__ == "__main__":
    main()
