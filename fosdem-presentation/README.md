# BadgeFed FOSDEM Presentation

This directory contains the FOSDEM 2025 presentation about **Decentralised Badges with BadgeFed: Implementing ActivityPub-based Credentials for Non-Profits**.

## Files

- `badgefed-fosdem.md` - Main presentation slides in Marp format
- `badgefed-theme.css` - Custom styling theme 
- `package.json` - Node.js dependencies and build scripts
- `marp.config.yml` - Marp configuration

## Prerequisites

Install Node.js and npm, then install Marp CLI:

```bash
npm install
```

Or install globally:
```bash
npm install -g @marp-team/marp-cli
```

## Usage

### Preview the presentation
```bash
npm run serve
```
This will start a local server and open the presentation in your browser with live reload.

### Build PDF
```bash
npm run build
```
Generates `badgefed-fosdem.pdf`

### Build HTML
```bash
npm run build-html  
```
Generates `badgefed-fosdem.html`

### Preview mode
```bash
npm run preview
```
Opens the presentation in preview mode.

## Presentation Content

The presentation covers:

1. **Problem Statement** - Why traditional badge platforms fail nonprofits
2. **BadgeFed Solution** - Open-source, federated alternative
3. **Technical Implementation** - ActivityPub + Open Badges integration
4. **Real-world Impact** - SOMOS.tech and Community Credentials success stories
5. **Future Roadmap** - What's next for federated credentialing

## Key Messages

- **Cost-effective** alternative to expensive commercial platforms
- **Federation prevents vendor lock-in** and ensures data sovereignty  
- **Standards-based approach** using ActivityPub and Open Badges
- **Real nonprofit impact** with SOMOS.tech case study
- **Community-driven development** and sustainable ecosystem

## Customization

Edit `badgefed-fosdem.md` to modify the presentation content. The presentation uses:

- **Marp** for slide generation
- **Custom CSS theme** in `badgefed-theme.css`
- **Gaia base theme** with BadgeFed branding
- **Mermaid diagrams** for technical architecture

## Resources Referenced

- BadgeFed: https://github.com/tryvocalcat/badgefed
- Community Credentials: https://communitycredentials.org  
- SOMOS.tech: https://somos.tech
- ActivityPub: https://w3.org/TR/activitypub
- Open Badges: https://openbadges.org

## License

This presentation is released under the same LGPL license as BadgeFed.