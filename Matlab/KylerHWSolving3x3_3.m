% Define grid
[x, y] = meshgrid(-5:0.5:5, -5:0.5:5);

% Calculate z for each plane
z1 = 3 - x - y;                    % x + y + z = 3
z2 = (16 - 2*x + y) / 3;          % 2x - y + 3z = 16
z3 = (9 - 3*x - y) / 2;           % 3x + y + 2z = 9

% Plot
figure;
hold on;
surf(x, y, z1, 'FaceAlpha', 0.5, 'EdgeColor', 'none', 'FaceColor', 'r');
surf(x, y, z2, 'FaceAlpha', 0.5, 'EdgeColor', 'none', 'FaceColor', 'g');
surf(x, y, z3, 'FaceAlpha', 0.5, 'EdgeColor', 'none', 'FaceColor', 'b');

% Formatting
xlabel('x');
ylabel('y');
zlabel('z');
title('Three Planes Intersection');
legend('x+y+z=3', '2x-y+3z=16', '3x+y+2z=9');
grid on;
view(3);
axis equal;
hold off;