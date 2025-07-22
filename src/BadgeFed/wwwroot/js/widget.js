/*
 * BadgeFed Widget - Embed your badges anywhere
 * Version: 1.0.0
 * 
 * Usage:
 * 1. Include this script: <script src="https://your-badgefed-instance.com/js/widget.js"></script>
 * 2. Add a container: <div id="my-badges" data-badgefed-recipient="YOUR_PROFILE_URL"></div>
 * 3. Or use JavaScript: BadgeFedWidget.render('my-badges', 'YOUR_PROFILE_URL', options);
 */

(function() {
    'use strict';
    
    // Configuration - this will be dynamically set by the server
    let BADGEFED_API_BASE = '';
    let BADGEFED_BASE_URL = '';
    
    // Auto-detect base URL from script src
    (function detectBaseUrl() {
        const scripts = document.getElementsByTagName('script');
        for (let script of scripts) {
            if (script.src && script.src.includes('/js/widget.js')) {
                const url = new URL(script.src);
                BADGEFED_BASE_URL = `${url.protocol}//${url.host}`;
                BADGEFED_API_BASE = `${BADGEFED_BASE_URL}/api/embed`;
                break;
            }
        }
        
        // Fallback to current domain if not found
        if (!BADGEFED_BASE_URL) {
            BADGEFED_BASE_URL = window.location.origin;
            BADGEFED_API_BASE = `${BADGEFED_BASE_URL}/api/embed`;
        }
    })();
    
    // Default styles
    const DEFAULT_STYLES = `
        .badgefed-widget {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            background: #ffffff;
            border: 1px solid #e1e8ed;
            border-radius: 12px;
            padding: 24px;
            margin: 20px 0;
            box-shadow: 0 4px 16px rgba(0,0,0,0.1);
            max-width: 100%;
        }
        .badgefed-widget h3 {
            margin: 0 0 20px 0;
            color: #1a202c;
            font-size: 20px;
            font-weight: 700;
            text-align: center;
        }
        .badgefed-badges {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
            gap: 20px;
        }
        .badgefed-badge {
            border: 1px solid #e2e8f0;
            border-radius: 12px;
            padding: 20px;
            background: linear-gradient(145deg, #f8fafc 0%, #f1f5f9 100%);
            transition: all 0.3s ease;
            position: relative;
            overflow: hidden;
        }
        .badgefed-badge::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            height: 4px;
            background: linear-gradient(90deg, #3182ce, #805ad5);
        }
        .badgefed-badge:hover {
            transform: translateY(-4px);
            box-shadow: 0 8px 24px rgba(0,0,0,0.15);
            border-color: #3182ce;
        }
        .badgefed-badge-header {
            display: flex;
            align-items: center;
            margin-bottom: 16px;
        }
        .badgefed-badge-image {
            width: 72px;
            height: 72px;
            border-radius: 50%;
            object-fit: cover;
            margin-right: 16px;
            border: 3px solid #white;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }
        .badgefed-badge-info {
            flex: 1;
        }
        .badgefed-badge-title {
            font-weight: 700;
            font-size: 16px;
            color: #2d3748;
            margin-bottom: 6px;
            line-height: 1.3;
        }
        .badgefed-badge-issuer {
            font-size: 13px;
            color: #4a5568;
            font-weight: 500;
        }
        .badgefed-badge-description {
            font-size: 14px;
            color: #4a5568;
            line-height: 1.4;
            margin-bottom: 16px;
            display: -webkit-box;
            -webkit-line-clamp: 2;
            -webkit-box-orient: vertical;
            overflow: hidden;
        }
        .badgefed-badge-footer {
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .badgefed-badge-date {
            font-size: 12px;
            color: #718096;
            font-weight: 500;
        }
        .badgefed-badge-link {
            display: inline-flex;
            align-items: center;
            padding: 8px 16px;
            background: linear-gradient(135deg, #3182ce 0%, #2c5aa0 100%);
            color: white;
            text-decoration: none;
            border-radius: 8px;
            font-size: 13px;
            font-weight: 600;
            transition: all 0.2s ease;
            box-shadow: 0 2px 4px rgba(49, 130, 206, 0.3);
        }
        .badgefed-badge-link:hover {
            background: linear-gradient(135deg, #2c5aa0 0%, #2a4a7c 100%);
            transform: translateY(-1px);
            box-shadow: 0 4px 8px rgba(49, 130, 206, 0.4);
        }
        .badgefed-loading {
            text-align: center;
            padding: 60px 20px;
            color: #718096;
        }
        .badgefed-error {
            text-align: center;
            padding: 30px 20px;
            color: #e53e3e;
            background: linear-gradient(145deg, #fed7d7 0%, #feb2b2 100%);
            border-radius: 8px;
            border: 1px solid #fc8181;
        }
        .badgefed-spinner {
            border: 3px solid #e2e8f0;
            border-top: 3px solid #3182ce;
            border-radius: 50%;
            width: 32px;
            height: 32px;
            animation: badgefed-spin 1s linear infinite;
            margin: 0 auto 16px;
        }
        @keyframes badgefed-spin {
            0% { transform: rotate(0deg); }
            100% { transform: rotate(360deg); }
        }
        .badgefed-powered-by {
            margin-top: 24px;
            text-align: center;
            font-size: 12px;
            color: #718096;
            padding-top: 16px;
            border-top: 1px solid #e2e8f0;
        }
        .badgefed-powered-by a {
            color: #3182ce;
            text-decoration: none;
            font-weight: 600;
        }
        .badgefed-powered-by a:hover {
            text-decoration: underline;
        }
        .badgefed-external-tag {
            display: inline-block;
            background: #fbb6ce;
            color: #97266d;
            font-size: 10px;
            font-weight: 600;
            padding: 2px 6px;
            border-radius: 4px;
            text-transform: uppercase;
            margin-left: 8px;
        }
        .badgefed-local-tag {
            display: inline-block;
            background: #9ae6b4;
            color: #22543d;
            font-size: 10px;
            font-weight: 600;
            padding: 2px 6px;
            border-radius: 4px;
            text-transform: uppercase;
            margin-left: 8px;
        }
    `;
    
    // Inject styles
    function injectStyles() {
        if (document.getElementById('badgefed-styles')) return;
        
        const style = document.createElement('style');
        style.id = 'badgefed-styles';
        style.textContent = DEFAULT_STYLES;
        document.head.appendChild(style);
    }
    
    // Format date
    function formatDate(dateStr) {
        const date = new Date(dateStr);
        return date.toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric'
        });
    }
    
    // Truncate text
    function truncateText(text, maxLength) {
        if (!text || text.length <= maxLength) return text;
        return text.substring(0, maxLength).trim() + '...';
    }
    
    // Create badge HTML
    function createBadgeHTML(badge) {
        const viewUrl = badge.viewUrl.startsWith('http') 
            ? badge.viewUrl 
            : `${BADGEFED_BASE_URL}${badge.viewUrl}`;
            
        const statusTag = badge.isExternal 
            ? '<span class="badgefed-external-tag">External</span>'
            : '<span class="badgefed-local-tag">Local</span>';
            
        return `
            <div class="badgefed-badge">
                <div class="badgefed-badge-header">
                    <img src="${badge.image}" alt="${badge.imageAlt || badge.title}" class="badgefed-badge-image" />
                    <div class="badgefed-badge-info">
                        <div class="badgefed-badge-title">${truncateText(badge.title, 50)}</div>
                        <div class="badgefed-badge-issuer">${truncateText(badge.issuerName, 30)}${statusTag}</div>
                    </div>
                </div>
                ${badge.description ? `<div class="badgefed-badge-description">${truncateText(badge.description, 120)}</div>` : ''}
                <div class="badgefed-badge-footer">
                    <div class="badgefed-badge-date">Issued: ${formatDate(badge.issuedOn)}</div>
                    <a href="${viewUrl}" target="_blank" class="badgefed-badge-link">View Badge</a>
                </div>
            </div>
        `;
    }
    
    // Main widget function
    function renderBadgeWidget(containerId, recipient, options = {}) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error('BadgeFed Widget: Container not found:', containerId);
            return;
        }
        
        const title = options.title || 'My Badges';
        const limit = options.limit || 10;
        const showPoweredBy = options.showPoweredBy !== false;
        
        // Show loading state
        container.innerHTML = `
            <div class="badgefed-widget">
                <h3>${title}</h3>
                <div class="badgefed-loading">
                    <div class="badgefed-spinner"></div>
                    Loading badges...
                </div>
                ${showPoweredBy ? `<div class="badgefed-powered-by">Powered by <a href="${BADGEFED_BASE_URL}" target="_blank">BadgeFed</a></div>` : ''}
            </div>
        `;
        
        // Fetch badges
        const apiUrl = `${BADGEFED_API_BASE}/badges?recipient=${encodeURIComponent(recipient)}&limit=${limit}&format=simple`;
        
        fetch(apiUrl)
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                return response.json();
            })
            .then(badges => {
                if (!badges || badges.length === 0) {
                    container.innerHTML = `
                        <div class="badgefed-widget">
                            <h3>${title}</h3>
                            <div class="badgefed-error">No public badges found for this recipient.</div>
                            ${showPoweredBy ? `<div class="badgefed-powered-by">Powered by <a href="${BADGEFED_BASE_URL}" target="_blank">BadgeFed</a></div>` : ''}
                        </div>
                    `;
                    return;
                }
                
                const badgesHTML = badges.map(createBadgeHTML).join('');
                container.innerHTML = `
                    <div class="badgefed-widget">
                        <h3>${title}</h3>
                        <div class="badgefed-badges">
                            ${badgesHTML}
                        </div>
                        ${showPoweredBy ? `<div class="badgefed-powered-by">Powered by <a href="${BADGEFED_BASE_URL}" target="_blank">BadgeFed</a></div>` : ''}
                    </div>
                `;
            })
            .catch(error => {
                console.error('BadgeFed Widget Error:', error);
                container.innerHTML = `
                    <div class="badgefed-widget">
                        <h3>${title}</h3>
                        <div class="badgefed-error">Failed to load badges. Please try again later.</div>
                        ${showPoweredBy ? `<div class="badgefed-powered-by">Powered by <a href="${BADGEFED_BASE_URL}" target="_blank">BadgeFed</a></div>` : ''}
                    </div>
                `;
            });
    }
    
    // Initialize
    injectStyles();
    
    // Expose to global scope
    window.BadgeFedWidget = {
        render: renderBadgeWidget,
        version: '1.0.0'
    };
    
    // Auto-initialize widgets with data attributes
    document.addEventListener('DOMContentLoaded', function() {
        const widgets = document.querySelectorAll('[data-badgefed-recipient]');
        widgets.forEach(function(widget) {
            const recipient = widget.getAttribute('data-badgefed-recipient');
            const title = widget.getAttribute('data-badgefed-title') || 'My Badges';
            const limit = parseInt(widget.getAttribute('data-badgefed-limit')) || 10;
            const showPoweredBy = widget.getAttribute('data-badgefed-powered-by') !== 'false';
            
            if (recipient && widget.id) {
                renderBadgeWidget(widget.id, recipient, { title, limit, showPoweredBy });
            }
        });
    });
})();
