%% Interactive Ripple Ring Visualization with UI Controls
% Adjust count values and regenerate visualization
clear; close all;

%% Initial Wave Packet Sample Structure
global samples freq_labels colors frequency_radii
samples = struct(...
    'frequency', {0.0, 1.047, 2.094, 3.142, 4.189, 5.236}, ...  % R,Y,G,C,B,M
    'amplitude', {1.0, 1.0, 1.0, 1.0, 1.0, 1.0}, ...             % Always 1
    'phase', {0.0, 0.0, 0.0, 0.0, 0.0, 0.0}, ...                % Always 0
    'count', {20, 5, 30, 10, 40, 15} ...                         % User adjustable
);

%% Parameters
global nx ny grid_range x_range y_range max_radius ring_width count_scale
nx = 512;  % Increased resolution to avoid artifacts
ny = 512;
grid_range = 20;
x_range = [-grid_range, grid_range];
y_range = [-grid_range, grid_range];
max_radius = 20;  % Changed to 20 for black cutoff
ring_width = 1.2;
count_scale = 0.03;

% Map each frequency to a specific radius
frequency_radii = [15, 12.5, 10, 7.5, 5, 2.5];

% Color and label definitions
colors = [
    1, 0, 0;      % Red
    1, 1, 0;      % Yellow
    0, 1, 0;      % Green
    0, 1, 1;      % Cyan
    0, 0, 1;      % Blue
    1, 0, 1;      % Magenta
];
freq_labels = {'Red', 'Yellow', 'Green', 'Cyan', 'Blue', 'Magenta'};

%% Create main figure with UI controls
main_fig = figure('Position', [50, 50, 1600, 800], 'Name', 'Interactive Ripple Visualization');

% Create UI panel on the left
ui_panel = uipanel('Parent', main_fig, 'Position', [0.01, 0.01, 0.18, 0.98], ...
                   'Title', 'Frequency Controls', 'FontSize', 12);

% Create sliders and text displays for each frequency
global sliders text_displays
sliders = zeros(6, 1);
text_displays = zeros(6, 1);

for i = 1:6
    y_pos = 0.85 - (i-1) * 0.125;  % Adjusted spacing
    
    % Label
    uicontrol('Parent', ui_panel, 'Style', 'text', ...
              'String', freq_labels{i}, ...
              'ForegroundColor', colors(i,:), ...
              'Units', 'normalized', ...
              'Position', [0.05, y_pos, 0.25, 0.04], ...
              'FontWeight', 'bold', 'FontSize', 10);
    
    % Slider
    sliders(i) = uicontrol('Parent', ui_panel, 'Style', 'slider', ...
                          'Min', 0, 'Max', 50, ...
                          'Value', samples(i).count, ...
                          'Units', 'normalized', ...
                          'Position', [0.05, y_pos-0.04, 0.65, 0.03], ...
                          'Callback', @(src, evt) update_count_display(i));
    
    % Count display
    text_displays(i) = uicontrol('Parent', ui_panel, 'Style', 'text', ...
                                 'String', sprintf('%d', samples(i).count), ...
                                 'Units', 'normalized', ...
                                 'Position', [0.72, y_pos-0.02, 0.2, 0.04], ...
                                 'FontSize', 10);
end

% Add preset buttons (compact layout)
uicontrol('Parent', ui_panel, 'Style', 'pushbutton', ...
          'String', 'RGB Only', ...
          'Units', 'normalized', ...
          'Position', [0.05, 0.07, 0.4, 0.035], ...
          'Callback', @(src, evt) set_preset([20, 0, 30, 0, 40, 0]));

uicontrol('Parent', ui_panel, 'Style', 'pushbutton', ...
          'String', 'All Equal', ...
          'Units', 'normalized', ...
          'Position', [0.55, 0.07, 0.4, 0.035], ...
          'Callback', @(src, evt) set_preset([20, 20, 20, 20, 20, 20]));

uicontrol('Parent', ui_panel, 'Style', 'pushbutton', ...
          'String', 'Clear All', ...
          'Units', 'normalized', ...
          'Position', [0.05, 0.035, 0.4, 0.035], ...
          'Callback', @(src, evt) set_preset([0, 0, 0, 0, 0, 0]));

uicontrol('Parent', ui_panel, 'Style', 'pushbutton', ...
          'String', 'Random', ...
          'Units', 'normalized', ...
          'Position', [0.55, 0.035, 0.4, 0.035], ...
          'Callback', @(src, evt) set_preset(randi([0, 50], 1, 6)));

% Regenerate button
uicontrol('Parent', ui_panel, 'Style', 'pushbutton', ...
          'String', 'REGENERATE', ...
          'Units', 'normalized', ...
          'Position', [0.05, 0.001, 0.43, 0.035], ...
          'FontWeight', 'bold', 'FontSize', 11, ...
          'BackgroundColor', [0.2, 0.7, 0.2], ...
          'ForegroundColor', 'white', ...
          'Callback', @regenerate_visualization);

% Rotate view button
uicontrol('Parent', ui_panel, 'Style', 'pushbutton', ...
          'String', 'ROTATE VIEW', ...
          'Units', 'normalized', ...
          'Position', [0.52, 0.001, 0.43, 0.035], ...
          'FontWeight', 'bold', 'FontSize', 10, ...
          'BackgroundColor', [0.2, 0.2, 0.7], ...
          'ForegroundColor', 'white', ...
          'Callback', @(src, evt) rotate_view_callback());

% Create axes for visualization
global ax_3d ax_top ax_bar
ax_3d = axes('Parent', main_fig, 'Position', [0.22, 0.35, 0.5, 0.6]);
ax_top = axes('Parent', main_fig, 'Position', [0.75, 0.55, 0.22, 0.35]);
ax_bar = axes('Parent', main_fig, 'Position', [0.22, 0.05, 0.5, 0.25]);

% Generate initial visualization
regenerate_visualization();

%% Callback functions
function update_count_display(idx)
    global sliders text_displays samples
    count_val = round(get(sliders(idx), 'Value'));
    set(text_displays(idx), 'String', sprintf('%d', count_val));
    samples(idx).count = count_val;
end

function set_preset(values)
    global sliders text_displays samples
    for i = 1:6
        set(sliders(i), 'Value', values(i));
        set(text_displays(i), 'String', sprintf('%d', values(i)));
        samples(i).count = values(i);
    end
    regenerate_visualization();
end

function regenerate_visualization(~, ~)
    global samples ax_3d ax_top ax_bar
    global nx ny grid_range x_range y_range max_radius ring_width count_scale
    global freq_labels colors frequency_radii
    
    % Create spatial grid (use higher resolution to avoid artifacts)
    grid_size = max(nx, 512);
    x = linspace(x_range(1), x_range(2), grid_size);
    y = linspace(y_range(1), y_range(2), grid_size);
    [X, Y] = meshgrid(x, y);
    R = sqrt(X.^2 + Y.^2);
    
    % Create height field and color field
    height_field = zeros(size(R));
    color_field = zeros(grid_size, grid_size, 3);
    color_weights = zeros(size(R));
    
    % Create a ring for each frequency with non-zero count
    for s = 1:length(samples)
        if samples(s).count > 0
            ring_radius = frequency_radii(s);
            ring_height = samples(s).count * count_scale;
            ring_color = colors(s,:);
            
            % Gaussian ring profile (single peak per ring)
            ring_profile = exp(-((R - ring_radius).^2) / (2 * ring_width^2));
            
            % Simple ring contribution
            ring_contribution = ring_height * ring_profile;
            
            % Add to height field
            height_field = height_field + ring_contribution;
            
            % Add to color field
            for c = 1:3
                color_field(:,:,c) = color_field(:,:,c) + ring_color(c) * ring_contribution;
            end
            color_weights = color_weights + ring_contribution;
        end
    end
    
    % Normalize color field
    color_weights(color_weights == 0) = 1;
    for c = 1:3
        color_field(:,:,c) = color_field(:,:,c) ./ color_weights;
    end
    
    % Apply height-based intensity (using absolute value)
    height_norm = abs(height_field) / (max(abs(height_field(:))) + eps);
    for c = 1:3
        color_field(:,:,c) = color_field(:,:,c) .* (0.3 + 0.7 * height_norm);
    end
    
    % Make surface a flat grey plane at radius 20 and beyond
    outer_mask = R >= 20;
    height_field(outer_mask) = 0;  % Set to flat first
    grey_value = 0.6;  % Medium grey color
    
    % Set the outer region to be completely flat and grey
    for c = 1:3
        temp = color_field(:,:,c);
        temp(outer_mask) = grey_value;
        color_field(:,:,c) = temp;
    end
    
    % Plot 3D surface - both positive and negative (mirrored)
    axes(ax_3d);
    cla;
    hold on;
    
    % Plot positive surface
    surf(X, Y, height_field, color_field, 'EdgeColor', 'none', 'FaceColor', 'interp');
    
    % Plot negative surface (mirror)
    surf(X, Y, -height_field, color_field, 'EdgeColor', 'none', 'FaceColor', 'interp');
    
    % Ensure smooth shading
    shading interp;
    
    view(45, 20);  % 3/4 view to see double disc shape
    xlabel('x'); ylabel('y'); zlabel('Height');
    title('3D Double-Sided Disc (Click ROTATE VIEW for different angles)');
    lighting gouraud;
    light('Position', [2, 2, 3]);
    light('Position', [-2, -2, -3]);
    light('Position', [0, 0, -5]);  % Bottom center light
    axis tight;
    daspect([1 1 0.2]);  % Aspect ratio for disc
    grid on;
    
    zlim([-max([samples.count])*count_scale*1.5, max([samples.count])*count_scale*1.5]);
    hold off;
    
    % Plot top view with enhanced ring visibility
    axes(ax_top);
    cla;
    
    % Create a display version for top view
    height_display = height_field;
    height_display(outer_mask) = 0;  % Ensure flat outer region
    
    % Normalize and enhance contrast
    height_normalized = height_display / (max(height_display(:)) + eps);
    height_enhanced = height_normalized.^0.5;  % Power transform to enhance peaks
    
    imagesc(x, y, height_enhanced);
    
    % Use hot colormap with adjusted range for better contrast
    colormap(ax_top, hot(256));
    caxis([0, max(height_enhanced(:))]);  % Full range
    
    colorbar;
    title('Top View - 6 Bright Rings');
    xlabel('x'); ylabel('y');
    axis xy equal tight;
    hold on;
    
    % Add subtle ring markers
    for s = 1:length(samples)
        if samples(s).count > 0
            theta = linspace(0, 2*pi, 100);
            % Draw faint circle at exact ring position
            plot(frequency_radii(s)*cos(theta), frequency_radii(s)*sin(theta), ...
                 'w', 'LineWidth', 0.3, 'LineStyle', ':');
        end
    end
    
    % Draw boundary at radius 20
    theta = linspace(0, 2*pi, 100);
    plot(20*cos(theta), 20*sin(theta), 'Color', [0.5 0.5 0.5], 'LineWidth', 1);
    
    % Plot radial profile showing mirrored disc shape
    axes(ax_bar);
    cla;
    
    % Calculate radial profile (only up to radius 20)
    radial_dist = linspace(0, 20, 300);
    radial_height = zeros(size(radial_dist));
    
    for k = 1:length(radial_dist)
        r = radial_dist(k);
        height_at_r = 0;
        
        for s = 1:length(samples)
            if samples(s).count > 0
                ring_radius = frequency_radii(s);
                ring_height = samples(s).count * count_scale;
                
                % Single gaussian peak per ring
                ring_val = exp(-((r - ring_radius)^2) / (2 * ring_width^2));
                height_at_r = height_at_r + ring_height * ring_val;
            end
        end
        radial_height(k) = height_at_r;
    end
    
    % Plot both positive and negative profiles (exact mirror)
    hold on;
    fill([radial_dist, fliplr(radial_dist)], ...
         [radial_height, -fliplr(radial_height)], ...
         [0.8 0.8 0.9], 'EdgeColor', 'k', 'LineWidth', 1.5);
    
    % Plot center line
    yline(0, 'k-', 'LineWidth', 1);
    
    % Mark ring positions
    for s = 1:length(samples)
        if samples(s).count > 0
            xline(frequency_radii(s), '--', 'Color', colors(s,:), 'LineWidth', 0.5);
            peak_height = samples(s).count * count_scale;
            text(frequency_radii(s), peak_height*1.1, sprintf('%s', freq_labels{s}(1)), ...
                 'HorizontalAlignment', 'center', 'Color', colors(s,:), ...
                 'FontWeight', 'bold', 'FontSize', 10);
        end
    end
    
    xlabel('Distance from Center');
    ylabel('Height');
    title('Radial Cross-Section (Perfect Mirror)');
    xlim([0, 20]);
    ylim([-max([samples.count])*count_scale*1.3, max([samples.count])*count_scale*1.3]);
    grid on;
    
    % Update status
    active_rings = sum([samples.count] > 0);
    fprintf('Updated: %d active rings\n', active_rings);
end

function rotate_view_callback()
    global ax_3d
    axes(ax_3d);
    
    % Cycle through different views
    persistent view_index
    if isempty(view_index)
        view_index = 1;
    end
    
    views = [45, 20;   % 3/4 view
             0, 0;     % Side view  
             90, 0;    % Other side
             45, 90;   % Top view
             45, -20]; % Bottom angle
    
    view_index = mod(view_index, size(views, 1)) + 1;
    view(views(view_index, 1), views(view_index, 2));
end