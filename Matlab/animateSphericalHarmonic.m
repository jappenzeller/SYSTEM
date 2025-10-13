function animateSphericalHarmonic(l, m, worldRadius, fps)
    % Animate single harmonic with controls
    
    resolution = 60;
    
    % Grid setup
    theta = linspace(0, pi, resolution);
    phi = linspace(0, 2*pi, resolution);
    [THETA, PHI] = meshgrid(theta, phi);
    
    X = worldRadius * sin(THETA) .* cos(PHI);
    Y = worldRadius * cos(THETA);
    Z = worldRadius * sin(THETA) .* sin(PHI);
    
    % Compute harmonic
    Y_lm = computeSphericalHarmonic(l, m, THETA, PHI);
    max_val = max(abs(Y_lm(:)));
    
    % Figure with controls
    fig = figure('Position', [100, 100, 900, 800], ...
                 'Name', sprintf('Y_{%d}^{%d} Animation', l, m), ...
                 'NumberTitle', 'off');
    
    ax = axes('Position', [0.1, 0.15, 0.85, 0.8]);
    
    % Animation data
    animData = struct();
    animData.X = X;
    animData.Y = Y; 
    animData.Z = Z;
    animData.Y_lm = Y_lm;
    animData.max_val = max_val;
    animData.worldRadius = worldRadius;
    animData.phase = 0;
    animData.fps = fps;
    animData.isPlaying = false;
    animData.ax = ax;
    animData.l = l;
    animData.m = m;
    
    fig.UserData = animData;
    
    % Control panel
    controlPanel = uipanel('Position', [0, 0, 1, 0.1]);
    
    % Buttons
    playBtn = uicontrol(controlPanel, 'Style', 'pushbutton', ...
                       'String', '▶ Play', ...
                       'Units', 'normalized', ...
                       'Position', [0.02, 0.2, 0.08, 0.6], ...
                       'Callback', @(src, evt) playCallback(fig, src));
    
    pauseBtn = uicontrol(controlPanel, 'Style', 'pushbutton', ...
                        'String', '❚❚ Pause', ...
                        'Units', 'normalized', ...
                        'Position', [0.11, 0.2, 0.08, 0.6], ...
                        'Callback', @(src, evt) pauseCallback(fig), ...
                        'Enable', 'off');
    
    restartBtn = uicontrol(controlPanel, 'Style', 'pushbutton', ...
                          'String', '↺ Restart', ...
                          'Units', 'normalized', ...
                          'Position', [0.2, 0.2, 0.08, 0.6], ...
                          'Callback', @(src, evt) restartHarmonic(fig));
    
    % Display mode toggle
    modeBtn = uicontrol(controlPanel, 'Style', 'togglebutton', ...
                       'String', 'Real Part', ...
                       'Units', 'normalized', ...
                       'Position', [0.35, 0.2, 0.1, 0.6], ...
                       'Callback', @(src, evt) modeCallback(fig, src));
    
    % Phase slider
    phaseSlider = uicontrol(controlPanel, 'Style', 'slider', ...
                           'Min', 0, 'Max', 2*pi, 'Value', 0, ...
                           'Units', 'normalized', ...
                           'Position', [0.5, 0.2, 0.3, 0.6], ...
                           'Callback', @(src, evt) phaseCallback(fig, src));
    
    uicontrol(controlPanel, 'Style', 'text', ...
              'String', 'Phase:', ...
              'Units', 'normalized', ...
              'Position', [0.5, 0.7, 0.1, 0.2]);
    
    phaseText = uicontrol(controlPanel, 'Style', 'text', ...
                         'String', '0°', ...
                         'Units', 'normalized', ...
                         'Position', [0.7, 0.7, 0.1, 0.2]);
    
    % Store controls
    animData.playBtn = playBtn;
    animData.pauseBtn = pauseBtn;
    animData.phaseSlider = phaseSlider;
    animData.phaseText = phaseText;
    animData.modeBtn = modeBtn;
    animData.displayMode = 'real';  % 'real', 'imag', or 'abs'
    fig.UserData = animData;
    
    % Timer
    animTimer = timer('ExecutionMode', 'fixedRate', ...
                     'Period', 1/fps, ...
                     'TimerFcn', @(src, evt) updateHarmonic(fig));
    
    animData.timer = animTimer;
    fig.UserData = animData;
    
    % Initial draw
    updateHarmonic(fig);
    
    % Cleanup
%    fig.CloseRequestFcn = @(src, evt) closeAnimation(src, animTimer);
end

function updateHarmonic(fig)
    if ~isvalid(fig)
        return;
    end
    
    data = fig.UserData;
    
    % Calculate current value based on mode
    switch data.displayMode
        case 'real'
            current_value = real(data.Y_lm * exp(1i * data.phase));
            cmap = redblue(256);
            clims = [-data.max_val, data.max_val];
        case 'imag'
            current_value = imag(data.Y_lm * exp(1i * data.phase));
            cmap = redblue(256);
            clims = [-data.max_val, data.max_val];
        case 'abs'
            current_value = abs(data.Y_lm);
            cmap = 'jet';
            clims = [0, data.max_val];
    end
    
    % Update surface
    cla(data.ax);
    surf(data.ax, data.X, data.Y, data.Z, current_value, 'EdgeColor', 'none');
    
    % Formatting
    axis(data.ax, 'equal');
    axis(data.ax, [-data.worldRadius data.worldRadius ...
                   -data.worldRadius data.worldRadius ...
                   -data.worldRadius data.worldRadius] * 1.2);
    shading(data.ax, 'interp');
    colormap(data.ax, cmap);
    caxis(data.ax, clims);
    colorbar(data.ax);
    
    camlight;
    lighting gouraud;
    view(data.ax, 45, 30);
    
    title(data.ax, sprintf('Y_{%d}^{%d} - %s - Phase: %.0f°', ...
          data.l, data.m, data.displayMode, data.phase * 180/pi));
    
    % Update phase if playing
    if data.isPlaying
        data.phase = mod(data.phase + 0.1, 2*pi);
        data.phaseSlider.Value = data.phase;
        data.phaseText.String = sprintf('%.0f°', data.phase * 180/pi);
        fig.UserData = data;
    end
    
    drawnow;
end

function modeCallback(fig, btn)
    data = fig.UserData;
    if btn.Value
        btn.String = 'Imag Part';
        data.displayMode = 'imag';
    else
        btn.String = 'Real Part';
        data.displayMode = 'real';
    end
    fig.UserData = data;
    updateHarmonic(fig);
end

function phaseCallback(fig, slider)
    data = fig.UserData;
    data.phase = slider.Value;
    data.phaseText.String = sprintf('%.0f°', data.phase * 180/pi);
    fig.UserData = data;
    updateHarmonic(fig);
end

function restartHarmonic(fig)
    data = fig.UserData;
    data.phase = 0;
    data.phaseSlider.Value = 0;
    data.phaseText.String = '0°';
    fig.UserData = data;
    updateHarmonic(fig);
end

% Helper colormap
function cmap = redblue(n)
    r = [linspace(0, 1, n/2), ones(1, n/2)]';
    g = [linspace(0, 1, n/2), linspace(1, 0, n/2)]';
    b = [ones(1, n/2), linspace(1, 0, n/2)]';
    cmap = [r g b];
end