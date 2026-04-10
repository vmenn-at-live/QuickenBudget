/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
function configureScalesGetMargins(height, width, xScale, yScale, maxXTicks, maxYTicks, xFormat, yFormat, xPadding, yPadding)
{
    // Using the maximum number of possible labels determine max size of a label.
    const xF = xFormat ? xScale.tickFormat(maxXTicks, xFormat) : xScale.tickFormat();
    const xLabels = xScale.ticks(maxXTicks).map(xF);
    const xLabelSize = getMaxLabelSize(xLabels);

    // The labels on the Y axis depend on the vertical size of the label. We currently have only the vertical
    // size of the X-axis labels, but since we use the identical font, we should be able to use that for this
    // estimate. We only want to have a max number so we can get all possible labels (but not overly large).
    maxYTicks = maxYTicks || Math.floor(height / xLabelSize.height);

    // Y scale - amounts. No range yet.
    const yF = yFormat ? yScale.tickFormat(maxYTicks, yFormat) : yScale.tickFormat();
    const yLabels = yScale.ticks(maxYTicks).map(yF);
    const yLabelSize = getMaxLabelSize(yLabels);

    // Bottom (x) axis have labels below, so add their height.
    const marginBottom = xLabelSize.height + xPadding;
    // Left (y) axis have labels to the left, so add their width.
    const marginLeft = yLabelSize.width + yPadding;

    const marginRight = xLabelSize.width / 2;
    const marginTop = yLabelSize.height / 2;

    // Add range to scales now that we have all the margins computed.
    xScale.range([marginLeft, width - marginRight]);
    yScale.range([height - marginBottom, marginTop]);

    // Bottom (x) axis have labels below, so add their height.
    // Left (y) axis have labels to the left, so add their width.
    return {xLabelSize: xLabelSize, yLabelSize: yLabelSize, marginTop: marginTop, marginRight: marginRight, marginBottom: marginBottom, marginLeft: marginLeft};
}

function createSvg(containerName, width, height)
{
    // Ready to create the svg.
    return d3.select(`#${containerName}`).append("svg")
        .attr("width", width)
        .attr("height", height)
        .attr("viewBox", `0 0 ${width} ${height}`)
        .style("max-width", "100%");
}

class D3MouseToPageMapper {
    constructor(svg, xScale, yScale, xIsPrimary, data, coordinatesAccessor) {
        this.svg = svg;
        this.xScale = xScale;
        this.yScale = yScale;
        this.xIsPrimary = xIsPrimary;
        this.data = data;
        this.coordinatesAccessor = coordinatesAccessor;
        this.primaryCoordinateAccessor = (d) => this.coordinatesAccessor(d)[this.xIsPrimary ? 0 : 1];
    }

    findDataPoint(mx, my) {
        // Invert the mouse coordinate. Based on xIsPrimary flag use the appropriate scale to find the primary coordinate, then use bisector to find the closest data point
        const primaryCoordinate = this.xIsPrimary ? this.xScale.invert(mx) : this.yScale.invert(my);
        const index = d3.bisector(d => this.primaryCoordinateAccessor(d)).left(this.data, primaryCoordinate);
        if (index > 0 && index < this.data.length)
        {
            return this.primaryCoordinateAccessor(this.data[index]) > primaryCoordinate ? this.data[index - 1] : this.data[index];
        }
        else if (index == 0)
        {
            return this.data[0];
        }

        return this.data[index - 1];
    }

    mapEvent(event) {
        const [mx, my] = d3.pointer(event);
        // Get the closest data point to the mouse coordinates. If not found, return null.
        const dataPoint = this.findDataPoint(mx, my);
        if (dataPoint == null) return null;
        // Get the domain coordinates of the data point
        const [domainX, domainY] = this.coordinatesAccessor(dataPoint);

        return {
            x: this.xIsPrimary ? mx : this.xScale(domainX),
            y: !this.xIsPrimary ? my : this.yScale(domainY),
            html: dataPoint.html()
        };
    }

    // Show tooltip on a d3 chart from event using the mapper object.
    // It is used by line and area charts
    showTooltip(tooltipId, event) {
        let pos = this.mapEvent(event);
        if (pos != null && pos.y > 0) {
            // Convert from SVG-local coordinates to page coordinates for use with style left/top.
            const rect = this.svg.node().getBoundingClientRect();
            let x = window.scrollX + rect.left + pos.x;
            let y = window.scrollY + rect.top + pos.y;

            const tooltip = d3.select(`#${tooltipId}`);
            tooltip.style("display", "block");
            tooltip.html(pos.html);
            const tooltipRect = tooltip.node().getBoundingClientRect();

            // Now that tooltip's set up, we can see if we're outside svg bounding rect and move
            // tooltip coordinates in.
            if (this.xIsPrimary && pos.x + tooltipRect.width  > rect.width)
            {
                x -= tooltipRect.width;
            }
            else if (!this.xIsPrimary && pos.y + tooltipRect.height > rect.height)
            {
                y -= tooltipRect.height;
            }
            tooltip.style("left", x + "px")
                .style("top", y + "px");
        }
    }

    attachEvents(tooltipId)
    {
        this.svg.style("cursor", "pointer");
        this.svg.on("touchmove mousemove", event => this.showTooltip(tooltipId, event))
        this.svg.on("touchend mouseleave", () => hideTooltip(tooltipId))
    }
}
