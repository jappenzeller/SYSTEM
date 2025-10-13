function animateQuantumState(l_values, m_values, coefficients, worldRadius, fps)
    % Multiple harmonics with interactive controls
    
    resolution = 50;
    duration = 5;  % seconds
    
    % Pre-generate grid
    theta = linspace(0, pi, resolution);
    phi = linspace(0, 2*pi, resolution);
    [THETA, PHI] = meshgrid(theta, phi);
    
    % Cartesian coordinates
    X = worldRadius * sin(THETA) .* cos(PHI);
    Y = worldRadius * cos(THETA);
    Z = worldRadius * sin(THETA) .* sin(PHI);
    
    % Set up figure with controls
    fig = figure('Position', [100, 100, 900, 800], ...
                 'Name', 'Quantum State Animation', ...
                 'NumberTitle', 'off');
    
    % Create axes for 3D plot
    ax = axes('Position', [0.1, 0.15, 0.85, 0.8]);
    
    % Animation data structure
    animData = struct();
    animData.l_values = l_values;
    animData.m_values = m_values;
    animData.coefficients = coefficients;
    animData.X = X;
    animData.Y = Y;
    animData.Z = Z;
    animData.THETA = THETA;
    animData.PHI = PHI;
    animData.worldRadius = worldRadius;
    animData.currentTime = 0;
    animData.duration = duration;
    animData.fps = fps;
    animData.isPlaying = false;
    animData.ax = ax;
    
    % Store in figure
    fig.UserData = animData;
    
    % Create control buttons
    playBtn = uicontrol('Style', 'pushbutton', ...
                        'String', 'Play', ...
                        'Position', [20, 20, 60, 30], ...
                        'Callback', @(src, evt) playCallback(fig, src));
    
    pauseBtn = uicontrol('Style', 'pushbutton', ...
                         'String', 'Pause', ...
                         'Position', [90, 20, 60, 30], ...
                         'Callback', @(src, evt) pauseCallback(fig), ...
                         'Enable', 'off');
    
    restartBtn = uicontrol('Style', 'pushbutton', ...
                           'String', 'Restart', ...
                           'Position', [160, 20, 60, 30], ...
                           'Callback', @(src, evt) restartCallback(fig));
    
    % Speed control slider
    speedSlider = uicontrol('Style', 'slider', ...
                           'Min', 0.1, 'Max', 3, 'Value', 1, ...
                           'Position', [250, 20, 150, 30], ...
                           'Callback', @(src, evt) speedCallback(fig, src));
    
    uicontrol('Style', 'text', ...
              'String', 'Speed:', ...
              'Position', [250, 50, 50, 20]);
    
    speedText = uicontrol('Style', 'text', ...
                         'String', '1.0x', ...
                         'Position', [350, 50, 50, 20]);
    
    % Store UI elements
    animData.playBtn = playBtn;
    animData.pauseBtn = pauseBtn;
    animData.speedSlider = speedSlider;
    animData.speedText = speedText;
    animData.speedMultiplier = 1;
    fig.UserData = animData;
    
    % Create timer
    animTimer = timer('ExecutionMode', 'fixedRate', ...
                     'Period', 1/fps, ...
                     'TimerFcn', @(src, evt) updateAnimation(fig), ...
                     'StopFcn', @(src, evt) stopAnimation(fig));
    
    animData.timer = animTimer;
    fig.UserData = animData;
    
    % Initial draw
    updateAnimation(fig);
    
    % Clean up timer when figure closes
    fig.CloseRequestFcn = @(src, evt) closeAnimation(src, animTimer);
end

function updateAnimation(fig)
    if ~isvalid(fig)
        return;
    end
    
    data = fig.UserData;
    t = data.currentTime;
    
    % Compute superposition
    psi = zeros(size(data.THETA));
    for k = 1:length(data.l_values)
        Y_lm = computeSphericalHarmonic(data.l_values(k), data.m_values(k), ...
                                       data.THETA, data.PHI);
        omega = getEnergy(data.l_values(k), data.m_values(k));
        phase = exp(-1i * omega * t);
        psi = psi + data.coefficients(k) * Y_lm * phase;
    end
    
    % Clear and redraw
    cla(data.ax);
    
    % Plot surface
    surf(data.ax, data.X, data.Y, data.Z, abs(psi).^2, 'EdgeColor', 'none');
    
    % Formatting
    axis(data.ax, 'equal');
    axis(data.ax, [-data.worldRadius data.worldRadius ...
                   -data.worldRadius data.worldRadius ...
                   -data.worldRadius data.worldRadius] * 1.2);
    shading(data.ax, 'interp');
    colormap(data.ax, 'jet');
    
    % Fixed color scale
    maxVal = 0;
    for k = 1:length(data.l_values)
        maxVal = maxVal + abs(data.coefficients(k));
    end
    caxis(data.ax, [0 maxVal^2]);
    colorbar(data.ax);
    
    % Lighting and view
    camlight;
    lighting gouraud;
    view(data.ax, 30 + t*20, 30);
    
    % Title
    title(data.ax, sprintf('Quantum State: t = %.2f / %.2f', t, data.duration));
    xlabel(data.ax, 'X'); 
    ylabel(data.ax, 'Y'); 
    zlabel(data.ax, 'Z');
    
    % Update time
    if data.isPlaying
        data.currentTime = data.currentTime + (1/data.fps) * data.speedMultiplier;
        if data.currentTime > data.duration
            data.currentTime = data.currentTime - data.duration;  % Loop
        end
        fig.UserData = data;
    end
    
    drawnow;
end

function playCallback(fig, btn)
    data = fig.UserData;
    if ~data.isPlaying
        data.isPlaying = true;
        btn.Enable = 'off';
        data.pauseBtn.Enable = 'on';
        fig.UserData = data;
        start(data.timer);
    end
end

function pauseCallback(fig)
    data = fig.UserData;
    if data.isPlaying
        data.isPlaying = false;
        stop(data.timer);
        data.playBtn.Enable = 'on';
        data.pauseBtn.Enable = 'off';
        fig.UserData = data;
    end
end

function restartCallback(fig)
    data = fig.UserData;
    data.currentTime = 0;
    fig.UserData = data;
    updateAnimation(fig);
end

function speedCallback(fig, slider)
    data = fig.UserData;
    data.speedMultiplier = slider.Value;
    data.speedText.String = sprintf('%.1fx', slider.Value);
    fig.UserData = data;
end

function closeAnimation(fig, timer)
    if isvalid(timer)
        stop(timer);
        delete(timer);
    end
    delete(fig);
end

function E = getEnergy(l, m)
    % Energy eigenvalue
    E = (l + abs(m)) * 2 * pi / 5;
end